using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Polaris.AI.Authoring;
using Polaris.Content;

namespace Polaris.AI;

internal static class BehaviorRepository
{
    static readonly ContentCatalog<string, CompiledBehavior> Behaviors =
        new ContentCatalog<string, CompiledBehavior>(StringComparer.Ordinal, ContentConflictPolicy.ThrowImmediately);
    static readonly Dictionary<string, string> SourceIds = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    internal static bool TryCreate(string behaviorId, AIActor actor, IReadOnlyDictionary<string, object?>? overrides, out BehaviorRuntime? runtime)
    {
        runtime = null;
        if (!Behaviors.TryGet(behaviorId, out CompiledBehavior behavior)) return false;
        var attributes = new Dictionary<string, object?>(behavior.Defaults, StringComparer.Ordinal);
        if (overrides != null)
        {
            foreach (KeyValuePair<string, object?> pair in overrides)
            {
                if (string.IsNullOrWhiteSpace(pair.Key) || !behavior.AttributeTypes.TryGetValue(pair.Key, out string type) ||
                    !BehaviorValues.Matches(type, pair.Value)) return false;
                attributes[pair.Key] = pair.Value;
            }
        }
        runtime = new BehaviorRuntime(behavior, actor, attributes);
        return true;
    }

    internal static bool LoadFile(string path)
    {
        try
        {
            PaiDocument document = LoadWithImports(Path.GetFullPath(path), new HashSet<string>(StringComparer.OrdinalIgnoreCase));
            CompiledBehavior compiled = BehaviorCompiler.Compile(document);
            // 必须先查冲突再退旧 id：否则"某文件改成占用别人的 id"会先把它原来的 id 从目录里摘掉，
            // 然后注册失败，白白丢一份本来还好用的行为。
            if (IsOwnedByAnotherFile(compiled.Id, path))
                throw new InvalidOperationException($"Behavior id '{compiled.Id}' is already defined by another file in this layer.");
            if (SourceIds.TryGetValue(path, out string oldId) && oldId != compiled.Id) Behaviors.Remove(oldId);
            Behaviors.TryRegister(compiled.Id, compiled, path);
            SourceIds[path] = compiled.Id;
            AIActorRegistry.ReloadBehavior(compiled.Id);
            return true;
        }
        catch (Exception ex)
        {
            PolarisAPI.Errors.Report(ex, $"Loading .pai '{path}'");
            return false;
        }
    }

    static bool IsOwnedByAnotherFile(string behaviorId, string path)
        => SourceIds.Any(x => x.Value == behaviorId && !string.Equals(x.Key, path, StringComparison.OrdinalIgnoreCase));

    internal static void LoadEmbedded(Assembly assembly)
    {
        foreach (string name in assembly.GetManifestResourceNames().Where(x => x.EndsWith(".pai", StringComparison.OrdinalIgnoreCase)))
        {
            try
            {
                using Stream? stream = assembly.GetManifestResourceStream(name);
                if (stream == null) continue;
                using var reader = new StreamReader(stream);
                CompiledBehavior behavior = BehaviorCompiler.Compile(PaiJson.Parse(reader.ReadToEnd()));
                Behaviors.TryRegister(behavior.Id, behavior, assembly.FullName);
            }
            catch (Exception ex) { PolarisAPI.Errors.Report(ex, $"Loading embedded .pai '{name}'"); }
        }
    }

    static PaiDocument LoadWithImports(string path, HashSet<string> stack)
    {
        if (!stack.Add(path)) throw new InvalidOperationException($"Circular .pai import at '{path}'.");
        PaiDocument root = PaiJson.Load(path);
        string directory = Path.GetDirectoryName(path)!;
        foreach (string import in root.Imports.ToArray())
        {
            string importedPath = Path.GetFullPath(Path.Combine(directory, import));
            if (!importedPath.StartsWith(directory + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException($"Import escapes its document directory: '{import}'.");
            PaiDocument imported = LoadWithImports(importedPath, stack);
            foreach (PaiTree tree in imported.Trees)
            {
                if (root.Trees.Any(x => x.Id == tree.Id)) throw new InvalidOperationException($"Imported tree id '{tree.Id}' is duplicated.");
                root.Trees.Add(tree);
            }
            foreach (KeyValuePair<string, PaiBehaviorAttribute> attribute in imported.BehaviorAttributes)
                if (!root.BehaviorAttributes.ContainsKey(attribute.Key)) root.BehaviorAttributes.Add(attribute.Key, attribute.Value);
        }
        root.Imports.Clear();
        stack.Remove(path);
        return root;
    }
}

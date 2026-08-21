using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Polaris.AI.Authoring;

namespace Polaris.AI;

internal static class BehaviorRepository
{
    static readonly Dictionary<string, CompiledBehavior> Behaviors = new Dictionary<string, CompiledBehavior>(StringComparer.Ordinal);
    static readonly Dictionary<string, string> SourceIds = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    internal static bool TryCreate(string behaviorId, AIActor actor, IReadOnlyDictionary<string, object?>? overrides, out BehaviorRuntime? runtime)
    {
        runtime = null;
        if (!Behaviors.TryGetValue(behaviorId, out CompiledBehavior behavior)) return false;
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
            if (SourceIds.Any(x => !string.Equals(x.Key, path, StringComparison.OrdinalIgnoreCase) && x.Value == compiled.Id))
                throw new InvalidOperationException($"Behavior id '{compiled.Id}' is already defined by another file in this layer.");
            if (SourceIds.TryGetValue(path, out string oldId) && oldId != compiled.Id) Behaviors.Remove(oldId);
            Behaviors[compiled.Id] = compiled;
            SourceIds[path] = compiled.Id;
            AIActorRegistry.ReloadBehavior(compiled.Id);
            return true;
        }
        catch (Exception ex)
        {
            Polaris.PolarisAPI.Errors.Report(ex, $"Loading .pai '{path}'");
            return false;
        }
    }

    internal static void LoadDirectory(string directory)
    {
        if (!Directory.Exists(directory)) return;
        foreach (string path in Directory.EnumerateFiles(directory, "*.pai", SearchOption.AllDirectories)) LoadFile(path);
    }

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
                Behaviors[behavior.Id] = behavior;
            }
            catch (Exception ex) { Polaris.PolarisAPI.Errors.Report(ex, $"Loading embedded .pai '{name}'"); }
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

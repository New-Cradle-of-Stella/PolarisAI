using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using Polaris.AI.Authoring;

namespace Polaris.AI;

internal sealed class CompiledBehavior
{
    internal CompiledBehavior(string id, Func<BehaviorNode> createRoot, Dictionary<string, object?> defaults,
        Dictionary<string, string> attributeTypes)
    { Id = id; CreateRoot = createRoot; Defaults = defaults; AttributeTypes = attributeTypes; }
    internal string Id { get; }
    internal Func<BehaviorNode> CreateRoot { get; }
    internal Dictionary<string, object?> Defaults { get; }
    internal Dictionary<string, string> AttributeTypes { get; }
}

internal static class BehaviorCompiler
{
    internal static CompiledBehavior Compile(PaiDocument document)
    {
        IReadOnlyList<PaiDiagnostic> diagnostics = PaiValidator.Validate(document, PaiNodeCatalog.CreateBuiltIn());
        if (PaiValidator.HasErrors(diagnostics)) throw new InvalidOperationException(string.Join(Environment.NewLine, diagnostics));
        var trees = document.Trees.ToDictionary(x => x.Id, StringComparer.Ordinal);
        var defaults = document.BehaviorAttributes.ToDictionary(x => x.Key, x => BehaviorValues.FromJson(x.Value.Default), StringComparer.Ordinal);
        var types = document.BehaviorAttributes.ToDictionary(x => x.Key, x => x.Value.Type, StringComparer.Ordinal);
        return new CompiledBehavior(document.Id, () => CompileTree(trees[document.MainTree], trees, new HashSet<string>(StringComparer.Ordinal)), defaults, types);
    }

    static BehaviorNode CompileTree(PaiTree tree, Dictionary<string, PaiTree> trees, HashSet<string> stack)
    {
        if (!stack.Add(tree.Id)) throw new InvalidOperationException($"Recursive SubTree reference at '{tree.Id}'.");
        var nodes = tree.Nodes.ToDictionary(x => x.Id, StringComparer.Ordinal);
        BehaviorNode CompileNode(string id)
        {
            PaiNode node = nodes[id];
            var children = node.Children.Select(CompileNode).ToArray();
            return node.Type switch
            {
                "Sequence" => new SequenceNode(id, children),
                "Fallback" => new FallbackNode(id, children),
                "ReactiveSequence" => new ReactiveNode(id, children, true),
                "ReactiveFallback" => new ReactiveNode(id, children, false),
                "Parallel" => new ParallelNode(id, children),
                "WeightedSelector" => new WeightedSelectorNode(id, children),
                "Inverter" => new InverterNode(id, children[0]),
                "Succeeder" => new SucceederNode(id, children[0]),
                "Cooldown" => new TimedDecoratorNode(id, children[0], Number(node, "seconds", 0), true),
                "Timeout" => new TimedDecoratorNode(id, children[0], Number(node, "seconds", 0), false),
                "Repeat" => new RepeatNode(id, children[0], Integer(node, "count", -1)),
                "Chance" => new ChanceNode(id, children[0], Number(node, "probability", 1)),
                "SubTree" => CompileTree(trees[String(node, "tree")], trees, new HashSet<string>(stack, StringComparer.Ordinal)),
                "AlwaysSuccess" => new DelegateNode(id, _ => NodeStatus.Success),
                "AlwaysFailure" => new DelegateNode(id, _ => NodeStatus.Failure),
                "HasTarget" => new DelegateNode(id, c => c.Actor.Target != null ? NodeStatus.Success : NodeStatus.Failure),
                "ClearTarget" => new DelegateNode(id, c => { c.Actor.ClearTarget(); return NodeStatus.Success; }),
                "TargetInRange" => new DelegateNode(id, c => TargetInRange(c, Number(node, "range", 1)(c))),
                "FaceTarget" => new DelegateNode(id, FaceTarget),
                "MoveToTarget" => new MoveToTargetNode(id, Number(node, "speed", 0.08), Number(node, "stopDistance", 1)),
                "AttackTarget" => new AttackTargetNode(id, Number(node, "range", 1), Integer(node, "damage", 1), Number(node, "cooldown", 0.5)),
                "Wait" => new WaitNode(id, Number(node, "seconds", 0)),
                _ => throw new InvalidOperationException($"Unsupported behavior node '{node.Type}'."),
            };
        }
        BehaviorNode root = CompileNode(tree.Root);
        stack.Remove(tree.Id);
        return root;
    }

    static Func<BehaviorContext, double> Number(PaiNode node, string key, double fallback)
    {
        if (!node.Ports.TryGetValue(key, out JsonElement value)) return _ => fallback;
        if (value.ValueKind == JsonValueKind.Number) { double number = value.GetDouble(); return _ => number; }
        if (value.ValueKind == JsonValueKind.String && TryBinding(value.GetString(), out string attribute))
            return c => c.Attributes.TryGetValue(attribute, out object? raw) ? Convert.ToDouble(raw, CultureInfo.InvariantCulture) : fallback;
        throw new InvalidOperationException($"Port '{node.Id}.{key}' must be numeric or a behavior binding.");
    }

    static Func<BehaviorContext, int> Integer(PaiNode node, string key, int fallback)
    {
        Func<BehaviorContext, double> number = Number(node, key, fallback);
        return c => checked((int)number(c));
    }

    static string String(PaiNode node, string key)
    {
        if (!node.Ports.TryGetValue(key, out JsonElement value) || value.ValueKind != JsonValueKind.String)
            throw new InvalidOperationException($"Port '{node.Id}.{key}' must be a string.");
        return value.GetString()!;
    }

    static bool TryBinding(string? value, out string key)
    {
        const string prefix = "{behavior.";
        if (value != null && value.StartsWith(prefix, StringComparison.Ordinal) && value.EndsWith("}", StringComparison.Ordinal))
        {
            key = value.Substring(prefix.Length, value.Length - prefix.Length - 1);
            return key.Length > 0;
        }
        key = string.Empty;
        return false;
    }

    static NodeStatus TargetInRange(BehaviorContext context, double range)
    {
        var target = context.Actor.Target;
        if (target == null) return NodeStatus.Failure;
        double dx = target.X - context.Actor.X;
        double dy = target.Y - context.Actor.Y;
        return dx * dx + dy * dy <= range * range ? NodeStatus.Success : NodeStatus.Failure;
    }

    static NodeStatus FaceTarget(BehaviorContext context)
    {
        var target = context.Actor.Target;
        if (target == null) return NodeStatus.Failure;
        context.Actor.SetFacing(target.X >= context.Actor.X ? Polaris.API.GameFacing.Right : Polaris.API.GameFacing.Left);
        return NodeStatus.Success;
    }
}

internal sealed class BehaviorRuntime
{
    readonly BehaviorNode root;
    readonly BehaviorContext context;
    readonly Dictionary<string, string> attributeTypes;
    internal BehaviorRuntime(CompiledBehavior behavior, AIActor actor, Dictionary<string, object?> attributes)
    {
        BehaviorId = behavior.Id;
        Attributes = attributes;
        attributeTypes = behavior.AttributeTypes;
        root = behavior.CreateRoot();
        context = new BehaviorContext(actor, attributes, HashCode.Combine(behavior.Id, actor.Key ?? string.Empty));
    }
    internal string BehaviorId { get; }
    internal Dictionary<string, object?> Attributes { get; }
    internal NodeStatus LastStatus { get; private set; }
    internal NodeStatus Tick(float deltaTime)
    {
        context.DeltaTime = deltaTime;
        context.Time += Math.Max(0, deltaTime);
        return LastStatus = root.Tick(context);
    }
    internal void Abort(AbortReason reason) => root.Abort(context, reason);
    internal bool TrySetAttribute<T>(string key, T value)
    {
        if (string.IsNullOrWhiteSpace(key) || !attributeTypes.TryGetValue(key, out string type) || !BehaviorValues.Matches(type, value)) return false;
        Attributes[key] = value;
        return true;
    }
    internal bool TryRemoveAttribute(string key) => Attributes.Remove(key);
}

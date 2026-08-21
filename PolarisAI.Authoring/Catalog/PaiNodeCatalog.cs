using System;
using System.Collections.Generic;
using System.Linq;

namespace Polaris.AI.Authoring;

public enum PaiNodeKind
{
    Composite,
    Decorator,
    Action,
    Condition,
    SubTree,
}

public sealed class PaiNodeDescriptor
{
    public PaiNodeDescriptor(string type, string displayName, PaiNodeKind kind, params PaiPortDescriptor[] ports)
    {
        Type = type ?? throw new ArgumentNullException(nameof(type));
        DisplayName = displayName ?? type;
        Kind = kind;
        Ports = ports ?? Array.Empty<PaiPortDescriptor>();
    }

    public string Type { get; }
    public string DisplayName { get; }
    public PaiNodeKind Kind { get; }
    public IReadOnlyList<PaiPortDescriptor> Ports { get; }
}

public sealed class PaiPortDescriptor
{
    public PaiPortDescriptor(string name, string type, bool required = false)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Type = type ?? throw new ArgumentNullException(nameof(type));
        Required = required;
    }

    public string Name { get; }
    public string Type { get; }
    public bool Required { get; }
}

public sealed class PaiNodeCatalog
{
    readonly Dictionary<string, PaiNodeDescriptor> descriptors =
        new Dictionary<string, PaiNodeDescriptor>(StringComparer.Ordinal);

    public IEnumerable<PaiNodeDescriptor> Descriptors => descriptors.Values.OrderBy(x => x.Type, StringComparer.Ordinal);

    public PaiNodeCatalog Register(PaiNodeDescriptor descriptor)
    {
        if (descriptor == null) throw new ArgumentNullException(nameof(descriptor));
        descriptors[descriptor.Type] = descriptor;
        return this;
    }

    public bool TryGet(string type, out PaiNodeDescriptor descriptor)
        => descriptors.TryGetValue(type ?? string.Empty, out descriptor!);

    public static PaiNodeCatalog CreateBuiltIn()
    {
        var catalog = new PaiNodeCatalog();
        catalog.Register(new PaiNodeDescriptor("Sequence", "Sequence", PaiNodeKind.Composite));
        catalog.Register(new PaiNodeDescriptor("Fallback", "Fallback", PaiNodeKind.Composite));
        catalog.Register(new PaiNodeDescriptor("ReactiveSequence", "Reactive Sequence", PaiNodeKind.Composite));
        catalog.Register(new PaiNodeDescriptor("ReactiveFallback", "Reactive Fallback", PaiNodeKind.Composite));
        catalog.Register(new PaiNodeDescriptor("Parallel", "Parallel", PaiNodeKind.Composite));
        catalog.Register(new PaiNodeDescriptor("WeightedSelector", "Weighted Selector", PaiNodeKind.Composite));
        catalog.Register(new PaiNodeDescriptor("Inverter", "Inverter", PaiNodeKind.Decorator));
        catalog.Register(new PaiNodeDescriptor("Succeeder", "Succeeder", PaiNodeKind.Decorator));
        catalog.Register(new PaiNodeDescriptor("Cooldown", "Cooldown", PaiNodeKind.Decorator,
            new PaiPortDescriptor("seconds", "number", true)));
        catalog.Register(new PaiNodeDescriptor("Timeout", "Timeout", PaiNodeKind.Decorator,
            new PaiPortDescriptor("seconds", "number", true)));
        catalog.Register(new PaiNodeDescriptor("Repeat", "Repeat", PaiNodeKind.Decorator,
            new PaiPortDescriptor("count", "integer")));
        catalog.Register(new PaiNodeDescriptor("Chance", "Chance", PaiNodeKind.Decorator,
            new PaiPortDescriptor("probability", "number", true)));
        catalog.Register(new PaiNodeDescriptor("SubTree", "SubTree", PaiNodeKind.SubTree,
            new PaiPortDescriptor("tree", "string", true)));
        catalog.Register(new PaiNodeDescriptor("AlwaysSuccess", "Always Success", PaiNodeKind.Action));
        catalog.Register(new PaiNodeDescriptor("AlwaysFailure", "Always Failure", PaiNodeKind.Action));
        catalog.Register(new PaiNodeDescriptor("HasTarget", "Has Target", PaiNodeKind.Condition));
        catalog.Register(new PaiNodeDescriptor("ClearTarget", "Clear Target", PaiNodeKind.Action));
        catalog.Register(new PaiNodeDescriptor("TargetInRange", "Target In Range", PaiNodeKind.Condition,
            new PaiPortDescriptor("range", "number", true)));
        catalog.Register(new PaiNodeDescriptor("FaceTarget", "Face Target", PaiNodeKind.Action));
        catalog.Register(new PaiNodeDescriptor("MoveToTarget", "Move To Target", PaiNodeKind.Action,
            new PaiPortDescriptor("speed", "number", true),
            new PaiPortDescriptor("stopDistance", "number", true)));
        catalog.Register(new PaiNodeDescriptor("AttackTarget", "Attack Target", PaiNodeKind.Action,
            new PaiPortDescriptor("range", "number", true),
            new PaiPortDescriptor("damage", "integer", true),
            new PaiPortDescriptor("cooldown", "number", true)));
        catalog.Register(new PaiNodeDescriptor("Wait", "Wait", PaiNodeKind.Action,
            new PaiPortDescriptor("seconds", "number", true)));
        return catalog;
    }
}

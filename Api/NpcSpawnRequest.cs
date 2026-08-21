using System;
using System.Collections.Generic;
using Polaris.API;

namespace Polaris.AI;

public sealed class NpcSpawnRequest
{
    readonly Dictionary<string, object?> behaviorAttributes = new Dictionary<string, object?>(StringComparer.Ordinal);

    NpcSpawnRequest(string definitionId, GameMap map, GameVector2? position, string? anchorKey)
    {
        if (string.IsNullOrWhiteSpace(definitionId)) throw new ArgumentException("Definition id cannot be empty.", nameof(definitionId));
        Map = map ?? throw new ArgumentNullException(nameof(map));
        if (anchorKey != null && string.IsNullOrWhiteSpace(anchorKey)) throw new ArgumentException("Anchor key cannot be empty.", nameof(anchorKey));
        DefinitionId = definitionId;
        Position = position;
        AnchorKey = anchorKey;
    }

    public static NpcSpawnRequest At(string definitionId, GameMap map, GameVector2 position)
        => new NpcSpawnRequest(definitionId, map, position, null);

    public static NpcSpawnRequest AtAnchor(string definitionId, GameMap map, string anchorKey)
        => new NpcSpawnRequest(definitionId, map, null, anchorKey);

    public string DefinitionId { get; }
    public GameMap Map { get; }
    public GameVector2? Position { get; }
    public string? AnchorKey { get; }
    public string? InstanceKey { get; private set; }
    public GameFacing? Facing { get; private set; }
    public string? BehaviorId { get; private set; }
    public string? Faction { get; private set; }
    public NpcPlacementMode Placement { get; private set; }
    public IReadOnlyDictionary<string, object?> BehaviorAttributes => behaviorAttributes;

    public NpcSpawnRequest WithKey(string instanceKey)
    {
        if (string.IsNullOrWhiteSpace(instanceKey)) throw new ArgumentException("Instance key cannot be empty.", nameof(instanceKey));
        InstanceKey = instanceKey;
        return this;
    }

    public NpcSpawnRequest WithFacing(GameFacing facing) { Facing = facing; return this; }

    public NpcSpawnRequest WithBehavior(string behaviorId)
    {
        if (string.IsNullOrWhiteSpace(behaviorId)) throw new ArgumentException("Behavior id cannot be empty.", nameof(behaviorId));
        BehaviorId = behaviorId;
        return this;
    }

    public NpcSpawnRequest WithBehaviorAttribute<T>(string key, T value)
    {
        if (string.IsNullOrWhiteSpace(key)) throw new ArgumentException("Behavior attribute key cannot be empty.", nameof(key));
        if (!BehaviorValues.IsSupported(value)) throw new ArgumentException("The behavior attribute value is not a supported scalar or Core value object.", nameof(value));
        behaviorAttributes[key] = value;
        return this;
    }

    public NpcSpawnRequest WithFaction(string faction)
    {
        if (string.IsNullOrWhiteSpace(faction)) throw new ArgumentException("Faction cannot be empty.", nameof(faction));
        Faction = faction;
        return this;
    }

    public NpcSpawnRequest WithPlacement(NpcPlacementMode placement)
    {
        if (!Enum.IsDefined(typeof(NpcPlacementMode), placement)) throw new ArgumentOutOfRangeException(nameof(placement));
        Placement = placement;
        return this;
    }
}

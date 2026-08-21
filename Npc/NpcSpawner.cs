using System;
using System.Collections.Generic;
using Polaris.API;

namespace Polaris.AI;

internal sealed class NpcSpawnResult
{
    internal NpcSpawnResult(GameCharacter character, Action despawn, Action<bool>? setVisible = null,
        string? defaultBehavior = null, string? defaultFaction = null)
    {
        Character = character;
        Despawn = despawn;
        SetVisible = setVisible;
        DefaultBehavior = defaultBehavior;
        DefaultFaction = defaultFaction;
    }
    internal GameCharacter Character { get; }
    internal Action Despawn { get; }
    internal Action<bool>? SetVisible { get; }
    internal string? DefaultBehavior { get; }
    internal string? DefaultFaction { get; }
}

internal interface INpcBodyProvider
{
    bool CanSpawn(string definitionId);
    NpcSpawnResult? Spawn(NpcSpawnRequest request, GameVector2 position);
}

internal static class NpcSpawner
{
    static readonly List<INpcBodyProvider> Providers = new List<INpcBodyProvider>
    {
        new PnpcNpcProvider(),
        new CustomNpcProvider(),
        new ShadowNoelNpcProvider(),
        new CityCasterNpcProvider(),
    };
    static long nextKey;

    internal static AINpc? Spawn(NpcSpawnRequest request)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));
        if (!request.Map.IsValid || !ReferenceEquals(request.Map, Polaris.PolarisAPI.Game.World.CurrentMap))
            return Fail("The target map is not the current writable map.");
        if (request.Placement == NpcPlacementMode.SnapToGround)
            return Fail("SnapToGround is not enabled until the collision query has passed runtime validation.");
        if (request.AnchorKey != null && !request.Map.HasAnchor(request.AnchorKey))
            return Fail($"Map anchor '{request.AnchorKey}' does not exist.");

        string key = request.InstanceKey ?? $"{request.DefinitionId}#{System.Threading.Interlocked.Increment(ref nextKey)}";
        if (AIActorRegistry.FindNpc(key, request.Map) != null) return Fail($"NPC instance key '{key}' is already in use.");
        if (request.InstanceKey == null) request.WithKey(key);
        INpcBodyProvider? provider = Providers.Find(x => x.CanSpawn(request.DefinitionId));
        if (provider == null) return Fail($"NPC definition '{request.DefinitionId}' is not registered.");

        try
        {
            NpcSpawnResult? result = provider.Spawn(request, request.Position ?? GameVector2.Zero);
            if (result?.Character?.IsValid != true) return Fail($"NPC definition '{request.DefinitionId}' could not be created.");
            var npc = new AINpc(result.Character, request.Map, key, request.Faction ?? result.DefaultFaction,
                result.Despawn, result.SetVisible);
            if (!AIActorRegistry.AddNpc(npc)) { result.Despawn(); return Fail($"NPC instance key '{key}' is already in use."); }
            if (request.AnchorKey != null && !npc.MoveToAnchor(request.AnchorKey)) { npc.Despawn(); return Fail($"NPC could not be moved to anchor '{request.AnchorKey}'."); }
            if (request.Facing.HasValue) npc.SetFacing(request.Facing.Value, true);
            string? behaviorId = request.BehaviorId ?? result.DefaultBehavior;
            if (behaviorId != null && !npc.AttachBehavior(behaviorId, request.BehaviorAttributes))
            {
                npc.Despawn();
                return Fail($"Behavior '{behaviorId}' could not be attached to NPC '{key}'.");
            }
            return npc;
        }
        catch (Exception ex)
        {
            Polaris.PolarisAPI.Errors.Report(ex, $"PolarisAI spawn '{request.DefinitionId}'");
            return null;
        }
    }

    static AINpc? Fail(string message)
    {
        Polaris.PolarisAPI.Errors.Report(new InvalidOperationException(message), "PolarisAI.Npcs.Spawn");
        return null;
    }
}

using System;
using System.Collections.Generic;
using Polaris.API;

namespace Polaris.AI;

internal static class AIActorRegistry
{
    static readonly Dictionary<GameCharacter, AIActor> Actors = new Dictionary<GameCharacter, AIActor>();
    static readonly Dictionary<string, AINpc> Npcs = new Dictionary<string, AINpc>(StringComparer.Ordinal);

    internal static AIActor? Attach(GameCharacter? character)
    {
        if (character?.IsValid != true) return null;
        if (Actors.TryGetValue(character, out AIActor existing) && existing.IsValid) return existing;
        var actor = new AIActor(character, PolarisAPI.Game.World.CurrentMap);
        Actors[character] = actor;
        return actor;
    }

    internal static AIActor? Find(GameCharacter? character)
        => character != null && Actors.TryGetValue(character, out AIActor actor) && actor.IsValid ? actor : null;

    internal static bool Controls(GameCharacter? character) => Find(character)?.ControlsNativeAI == true;

    internal static AINpc? FindNpc(string instanceKey, GameMap? map)
    {
        if (string.IsNullOrWhiteSpace(instanceKey)) return null;
        if (!Npcs.TryGetValue(instanceKey, out AINpc npc) || !npc.IsValid) return null;
        GameMap? targetMap = map ?? PolarisAPI.Game.World.CurrentMap;
        return targetMap != null && ReferenceEquals(npc.Map, targetMap) ? npc : null;
    }

    internal static IReadOnlyList<AIActor> GetActors(GameMap? map) => OnMap(Actors.Values, map);

    internal static IReadOnlyList<AINpc> GetNpcs(GameMap? map) => OnMap(Npcs.Values, map);

    /// <summary>筛出仍然有效、且挂在目标地图上的条目；<paramref name="map"/> 省略时用当前地图，没有当前地图则视为空。</summary>
    static IReadOnlyList<T> OnMap<T>(IEnumerable<T> candidates, GameMap? map) where T : AIActor
    {
        var result = new List<T>();
        GameMap? targetMap = map ?? PolarisAPI.Game.World.CurrentMap;
        if (targetMap == null) return result;
        foreach (T candidate in candidates)
            if (candidate.IsValid && ReferenceEquals(candidate.Map, targetMap)) result.Add(candidate);
        return result;
    }

    internal static bool AddNpc(AINpc npc)
    {
        if (Npcs.ContainsKey(npc.InstanceKey)) return false;
        Npcs.Add(npc.InstanceKey, npc);
        if (npc.Character != null) Actors[npc.Character] = npc;
        return true;
    }

    internal static void RemoveNpc(AINpc npc, GameCharacter character)
    {
        Npcs.Remove(npc.InstanceKey);
        Actors.Remove(character);
    }

    internal static bool Detach(GameCharacter? character)
    {
        if (character == null || !Actors.TryGetValue(character, out AIActor actor) || actor is AINpc) return false;
        Actors.Remove(character);
        actor.Release(AbortReason.Detached);
        return true;
    }

    internal static void Tick(float deltaTime)
    {
        var snapshot = new List<AIActor>(Actors.Values);
        foreach (AIActor actor in snapshot) actor.Tick(deltaTime);
        Sweep();
    }

    internal static void Shutdown()
    {
        foreach (AIActor actor in new List<AIActor>(Actors.Values)) actor.Release(AbortReason.Shutdown);
        Actors.Clear();
        Npcs.Clear();
    }

    internal static void ReloadBehavior(string behaviorId)
    {
        foreach (AIActor actor in new List<AIActor>(Actors.Values)) actor.ReloadBehavior(behaviorId);
    }

    static void Sweep()
    {
        foreach (KeyValuePair<GameCharacter, AIActor> pair in new List<KeyValuePair<GameCharacter, AIActor>>(Actors))
            if (!pair.Value.IsValid) Actors.Remove(pair.Key);
        foreach (KeyValuePair<string, AINpc> pair in new List<KeyValuePair<string, AINpc>>(Npcs))
            if (!pair.Value.IsValid) Npcs.Remove(pair.Key);
    }
}

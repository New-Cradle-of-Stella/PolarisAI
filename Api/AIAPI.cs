using System.Collections.Generic;
using Polaris.API;

namespace Polaris.AI;

public static class AIAPI
{
    public static class Npcs
    {
        public static AINpc? Spawn(string definitionId, GameMap map, GameVector2 position, string? instanceKey = null)
        {
            NpcSpawnRequest request = NpcSpawnRequest.At(definitionId, map, position);
            if (instanceKey != null) request.WithKey(instanceKey);
            return Spawn(request);
        }

        public static AINpc? SpawnAtAnchor(string definitionId, GameMap map, string anchorKey, string? instanceKey = null)
        {
            NpcSpawnRequest request = NpcSpawnRequest.AtAnchor(definitionId, map, anchorKey);
            if (instanceKey != null) request.WithKey(instanceKey);
            return Spawn(request);
        }

        public static AINpc? Spawn(NpcSpawnRequest request) => NpcSpawner.Spawn(request);
        public static AINpc? Find(string instanceKey, GameMap? map = null) => AIActorRegistry.FindNpc(instanceKey, map);
        public static IReadOnlyList<AINpc> GetAll(GameMap? map = null) => AIActorRegistry.GetNpcs(map);
    }

    public static class Actors
    {
        public static AIActor? Attach(GameCharacter character) => AIActorRegistry.Attach(character);
        public static AIActor? Find(GameCharacter character) => AIActorRegistry.Find(character);
        public static IReadOnlyList<AIActor> GetAll(GameMap? map = null) => AIActorRegistry.GetActors(map);
        public static bool Detach(GameCharacter character) => AIActorRegistry.Detach(character);
    }
}

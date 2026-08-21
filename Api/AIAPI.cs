using System.Collections.Generic;
using Polaris.API;

namespace Polaris.AI;

public static class AIAPI
{
    public static class Npcs
    {
        public static AINpc? Spawn(string definitionId, GameMap map, GameVector2 position, string? instanceKey = null)
            => SpawnWithOptionalKey(NpcSpawnRequest.At(definitionId, map, position), instanceKey);

        public static AINpc? SpawnAtAnchor(string definitionId, GameMap map, string anchorKey, string? instanceKey = null)
            => SpawnWithOptionalKey(NpcSpawnRequest.AtAnchor(definitionId, map, anchorKey), instanceKey);

        public static AINpc? Spawn(NpcSpawnRequest request) => NpcSpawner.Spawn(request);
        public static AINpc? Find(string instanceKey, GameMap? map = null) => AIActorRegistry.FindNpc(instanceKey, map);
        public static IReadOnlyList<AINpc> GetAll(GameMap? map = null) => AIActorRegistry.GetNpcs(map);

        static AINpc? SpawnWithOptionalKey(NpcSpawnRequest request, string? instanceKey)
        {
            if (instanceKey != null) request.WithKey(instanceKey);
            return NpcSpawner.Spawn(request);
        }
    }

    public static class Actors
    {
        public static AIActor? Attach(GameCharacter character) => AIActorRegistry.Attach(character);
        public static AIActor? Find(GameCharacter character) => AIActorRegistry.Find(character);
        public static IReadOnlyList<AIActor> GetAll(GameMap? map = null) => AIActorRegistry.GetActors(map);
        public static bool Detach(GameCharacter character) => AIActorRegistry.Detach(character);
    }
}

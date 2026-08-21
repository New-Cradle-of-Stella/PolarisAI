using m2d;
using nel;
using Polaris.API;
using UnityEngine;

namespace Polaris.AI;

internal sealed class CityCasterNpcProvider : INpcBodyProvider
{
    public bool CanSpawn(string definitionId)
        => definitionId == "citycaster.default" || definitionId == "citycaster.td";

    public NpcSpawnResult? Spawn(NpcSpawnRequest request, GameVector2 position)
    {
        NelM2DBase? game = M2DBase.Instance as NelM2DBase;
        CityCasterManager? manager = game?.CITYCM;
        if (manager == null) return null;
        string aiKey = request.DefinitionId == "citycaster.td" ? "td" : string.Empty;
        if (!manager.Summon(position.X, position.Y, aiKey, out M2CityCaster mover)) return null;
        GameCharacter character = GameCharacter.Wrap(mover);
        return new NpcSpawnResult(character, mover.destruct,
            visible => SetVisible(mover, visible), defaultFaction: "player");
    }

    static void SetVisible(M2Mover mover, bool visible)
    {
        foreach (Renderer renderer in mover.gameObject.GetComponentsInChildren<Renderer>(true)) renderer.enabled = visible;
    }
}

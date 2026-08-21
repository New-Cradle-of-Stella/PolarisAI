using m2d;
using nel;
using Polaris.API;

namespace Polaris.AI;

internal sealed class CityCasterNpcProvider : INpcBodyProvider
{
    public bool CanSpawn(string definitionId)
        => definitionId == BuiltInNpcIds.CityCasterDefault || definitionId == BuiltInNpcIds.CityCasterTd;

    public NpcSpawnResult? Spawn(NpcSpawnRequest request, GameVector2 position)
    {
        NelM2DBase? game = M2DBase.Instance as NelM2DBase;
        CityCasterManager? manager = game?.CITYCM;
        if (manager == null) return null;
        string aiKey = request.DefinitionId == BuiltInNpcIds.CityCasterTd ? "td" : string.Empty;
        if (!manager.Summon(position.X, position.Y, aiKey, out M2CityCaster mover)) return null;
        GameCharacter character = GameCharacter.Wrap(mover);
        return new NpcSpawnResult(character, mover.destruct,
            visible => NpcBodyVisibility.Set(mover, visible), defaultFaction: "player");
    }
}

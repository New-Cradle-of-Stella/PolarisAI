using System;
using m2d;
using Polaris.AI.Registration;
using Polaris.API;
using Polaris.Res.Pxls;

namespace Polaris.AI;

internal sealed class PnpcNpcProvider : INpcBodyProvider
{
    public bool CanSpawn(string definitionId) => PnpcRegistry.TryGet(definitionId, out _);

    public NpcSpawnResult? Spawn(NpcSpawnRequest request, GameVector2 position)
    {
        if (!PnpcRegistry.TryGet(request.DefinitionId, out PnpcSubmission submission)) return null;
        PxlsCharacterHandle handle = submission.Resource();
        if (handle == null) throw new InvalidOperationException($".pnpc '{request.DefinitionId}' PXLS resource has not been bound by PolarisRes yet.");
        if (handle.IsFaulted) throw new InvalidOperationException($".pnpc '{request.DefinitionId}' PXLS resource failed to load: {handle.Error?.Message}");
        if (!handle.IsReady || handle.Character == null)
            throw new InvalidOperationException($".pnpc '{request.DefinitionId}' PXLS resource is still loading.");

        Map2d? map = M2DBase.Instance?.curMap;
        if (map == null) return null;
        string key = request.InstanceKey ?? request.DefinitionId;
        PnpcDefinition definition = submission.Definition;
        PolarisPnpcBody body = map.createMover<PolarisPnpcBody>(key, position.X, position.Y);
        try
        {
            body.Configure(definition);
            body.appear(map);
            map.assignMover(body);
            body.BindCharacter(handle.Title, definition.InitialPose);
            return new NpcSpawnResult(GameCharacter.Wrap(body), body.destruct,
                visible => body.SetVisible(visible), definition.DefaultBehavior, definition.Faction);
        }
        catch
        {
            body.destruct();
            throw;
        }
    }
}

internal sealed class PolarisPnpcBody : M2AttackableP
{
    M2PxlAnimatorRT? animator;
    PnpcHitType hitType;

    internal void Configure(PnpcDefinition definition)
    {
        maxhp = definition.MaxHp;
        maxmp = definition.MaxMp;
        sizex = definition.Width * 0.5f;
        sizey = definition.Height * 0.5f;
        hitType = definition.HitType;
        floating = false;
        carryable_other_object = false;
    }

    internal void BindCharacter(string title, string pose)
    {
        animator = Mp.M2D.createBasicPxlAnimatorForRenderTicket(this, title, pose, true, DRAW_ORDER.PR0);
        animator.setAim((int)aim, 1);
        SpSetPose(pose, 0, string.Empty, true);
    }

    internal void SetVisible(bool visible)
    {
        if (animator != null) animator.alpha = visible ? 1f : 0f;
        NpcBodyVisibility.Set(this, visible);
    }

    public override bool isDamagingOrKo() => !is_alive;
    public override HITTYPE getHitType(M2Ray ray) => hitType switch
    {
        PnpcHitType.Player => HITTYPE.PR,
        PnpcHitType.Enemy => HITTYPE.EN,
        _ => HITTYPE.NONE,
    };

    public override void SpSetPose(string pose, int resetFrame = -1, string fixChange = "", bool forceAim = false)
    {
        if (animator == null) return;
        if (!string.IsNullOrEmpty(pose)) animator.setPose(pose, resetFrame);
        animator.setAim((int)aim, forceAim ? 1 : 0);
    }

    public override bool SpPoseIs(string pose) => animator?.poseIs(pose) == true;
    public override void SpMotionReset(int frame = 0) => animator?.animReset(frame);
}

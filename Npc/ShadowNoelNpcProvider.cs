using System;
using System.IO;
using System.Security.Cryptography;
using HarmonyLib;
using m2d;
using nel;
using Polaris.API;
using UnityEngine;

namespace Polaris.AI;

internal sealed class ShadowNoelNpcProvider : INpcBodyProvider
{
    public bool CanSpawn(string definitionId) => definitionId == BuiltInNpcIds.ShadowNoel;

    public NpcSpawnResult? Spawn(NpcSpawnRequest request, GameVector2 position)
    {
        if (!ShadowNoelCompatibility.IsSupported()) return null;
        NelM2DBase? game = M2DBase.Instance as NelM2DBase;
        Map2d? map = game?.curMap;
        if (game == null || map == null) return null;

        M2MoverPr? centerBefore = map.Pr;
        PRNoel? playerBefore = game.PlayerNoel;
        string key = request.InstanceKey ?? "polaris_shadow_noel";
        PolarisShadowNoel body = map.createMover<PolarisShadowNoel>(key, position.X, position.Y);
        body.InitializeAsNpc(key);
        if (body.gameObject.GetComponent<Rigidbody2D>() == null) body.gameObject.AddComponent<Rigidbody2D>();
        body.gameObject.name = key;
        map.assignMover(body);

        if (!ReferenceEquals(map.Pr, centerBefore) || !ReferenceEquals(game.PlayerNoel, playerBefore))
        {
            body.destruct();
            throw new InvalidOperationException("Background Noel attempted to replace the center player; creation was rolled back.");
        }

        GameCharacter character = GameCharacter.Wrap(body);
        return new NpcSpawnResult(character, body.destruct,
            visible => NpcBodyVisibility.Set(body, visible), defaultFaction: "player");
    }
}

internal static class ShadowNoelCompatibility
{
    const string SupportedHash = "C15AE0207DE38ACC80F055C219411B855BF8AE76B395234AEA046AAADB0248D9";
    static bool? supported;

    internal static bool IsSupported()
    {
        if (supported.HasValue) return supported.Value;
        try
        {
            string path = typeof(PR).Assembly.Location;
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return (supported = false).Value;
            using SHA256 sha = SHA256.Create();
            using FileStream stream = File.OpenRead(path);
            string hash = BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", string.Empty);
            return (supported = string.Equals(hash, SupportedHash, StringComparison.OrdinalIgnoreCase)).Value;
        }
        catch (Exception ex)
        {
            PolarisAPI.Errors.Report(ex, "Validating ShadowNoel game version");
            return (supported = false).Value;
        }
    }
}

internal sealed class PolarisShadowNoel : PRMain
{
    public override void createAnimator(ref PrAnimator animator)
    {
        M2PxlAnimatorRT nativeAnimator = Mp.M2D.createBasicPxlAnimatorForRenderTicket(
            this, "noel", "stand", false, DRAW_ORDER.PR1);
        if (animator == null)
        {
            SfPose = new AnimationShuffler(this);
            animator = new PrNoelAnimator(this, nativeAnimator, MTR.PConNoelAnim);
        }
        else
        {
            animator.initS(nativeAnimator);
        }
    }

    internal void InitializeAsNpc(string key)
    {
        newGame();
        this.key = key;
    }

    public override void newGame()
    {
        hp = maxhp = 150;
        mp = maxmp = 200;
        EpCon ??= new EpManager(this);
        if (VO == null)
        {
            VO = new PrVoiceController(this, MTR.VcNoelSource, snd_key + ".voice");
            BetoMng = BetobetoManager.GetManager("noel");
        }
        base.newGame();
        Ser.clear();
        EpCon.newGame();
        EggCon.newGame(false);
        GaugeBrk.reset();
        AbsorbCon = new AbsorbManagerContainer(5, this);
    }

    public override void appear(Map2d map)
    {
        base.appear(map);
        UP?.destruct();
        UP = null;
    }

    public override void refineMoveKey(bool ignoreKeyPushDown = false) { }
    public override bool runUi() => false;
    public override HITTYPE getHitType(M2Ray ray) => HITTYPE.EN;
}

[HarmonyPatch(typeof(Map2d), nameof(Map2d.assignCenterPlayer), new[] { typeof(M2MoverPr) })]
internal static class Patch_BackgroundNoel_AssignCenterPlayer
{
    // Harmony's __0 positional name is stable even when the game's metadata renames
    // assignCenterPlayer's first parameter (ver029 calls it "Mov", older builds used "Pr").
    static bool Prefix(M2MoverPr __0) => !(__0 is PolarisShadowNoel);
}

[HarmonyPatch(typeof(M2MoverPr), nameof(M2MoverPr.isEvadeO), new[] { typeof(int) })]
internal static class Patch_BackgroundNoel_EvadeInput
{
    static bool Prefix(M2MoverPr __instance, ref bool __result) => BackgroundNoelInput.None(__instance, ref __result);
}

[HarmonyPatch(typeof(M2MoverPr), nameof(M2MoverPr.isMagicO), new[] { typeof(int) })]
internal static class Patch_BackgroundNoel_MagicInput
{
    static bool Prefix(M2MoverPr __instance, ref bool __result) => BackgroundNoelInput.None(__instance, ref __result);
}

[HarmonyPatch(typeof(M2MoverPr), nameof(M2MoverPr.isAtkO), new[] { typeof(int) })]
internal static class Patch_BackgroundNoel_AttackInput
{
    static bool Prefix(M2MoverPr __instance, ref bool __result) => BackgroundNoelInput.None(__instance, ref __result);
}

[HarmonyPatch(typeof(M2MoverPr), nameof(M2MoverPr.isMagicStickO), new[] { typeof(int) })]
internal static class Patch_BackgroundNoel_MagicStickInput
{
    static bool Prefix(M2MoverPr __instance, ref bool __result) => BackgroundNoelInput.None(__instance, ref __result);
}

internal static class BackgroundNoelInput
{
    internal static bool None(M2MoverPr instance, ref bool result)
    {
        if (!(instance is PolarisShadowNoel)) return true;
        result = false;
        return false;
    }
}

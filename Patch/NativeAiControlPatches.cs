using HarmonyLib;
using nel;
using Polaris.API;

namespace Polaris.AI;

/// <summary>Only suppresses the native decision writer while an explicit Polaris behavior is enabled.</summary>
[HarmonyPatch(typeof(NAI), nameof(NAI.consider), new[] { typeof(float), typeof(float) })]
internal static class Patch_NAI_Consider
{
    static bool Prefix(NAI __instance)
        => !AIActorRegistry.Controls(GameCharacter.Wrap(__instance.En));
}

[HarmonyPatch(typeof(CityCasterAITD), nameof(CityCasterAITD.consider))]
internal static class Patch_CityCasterAITD_Consider
{
    static bool Prefix(CityCasterAITD __instance, ref string? __result)
    {
        if (!AIActorRegistry.Controls(GameCharacter.Wrap(__instance.Mv))) return true;
        __result = null;
        return false;
    }
}

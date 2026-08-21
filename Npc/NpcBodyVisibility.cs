using m2d;
using UnityEngine;

namespace Polaris.AI;

/// <summary>
/// NPC 躯体的显隐开关。原生 Mover 没有统一的可见性接口，只能按层级里的 <see cref="Renderer"/> 逐个开关；
/// 几个内置 provider 曾各自复制了同一份实现，这里合成一处。
/// </summary>
internal static class NpcBodyVisibility
{
    internal static void Set(M2Mover mover, bool visible)
    {
        foreach (Renderer renderer in mover.gameObject.GetComponentsInChildren<Renderer>(true)) renderer.enabled = visible;
    }
}

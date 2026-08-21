using System;
using System.Collections.Generic;

namespace Polaris.AI;

/// <summary>
/// PolarisAI 自带的 NPC 定义 id。这些 id 由本模块内置的 <see cref="INpcBodyProvider"/> 认领，
/// 因此对 <c>.pnpc</c> 注册是保留字——集中放一份，避免 provider 与 <see cref="PnpcRegistry"/> 各写一遍字面量。
/// </summary>
internal static class BuiltInNpcIds
{
    internal const string CustomBasic = "custom.basic";
    internal const string CityCasterDefault = "citycaster.default";
    internal const string CityCasterTd = "citycaster.td";
    internal const string ShadowNoel = "shadow.noel";

    static readonly HashSet<string> Reserved = new HashSet<string>(StringComparer.Ordinal)
    {
        CustomBasic, CityCasterDefault, CityCasterTd, ShadowNoel,
    };

    internal static bool IsReserved(string id) => Reserved.Contains(id);
}

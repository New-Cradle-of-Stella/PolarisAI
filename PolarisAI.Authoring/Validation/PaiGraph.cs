using System;
using System.Collections.Generic;

namespace Polaris.AI.Authoring;

/// <summary>
/// 以字符串为节点的图工具。校验里有两处需要查环——树内的父子边与树之间的 SubTree 引用边，
/// 两者曾各自写了一份等价的深度优先查环，这里合并成一份。
/// </summary>
static class PaiGraph
{
    /// <summary>
    /// 依次从 <paramref name="roots"/> 出发做深度优先遍历，找到第一个能走出环的起点。
    /// 已判定无环的节点在多个起点之间共享，因此整体是线性的。
    /// </summary>
    /// <param name="childrenOf">给出某个节点的出边；未知节点应返回空序列。</param>
    /// <param name="cycleAt">找到环时，是发现该环的那个起点；否则为空串。</param>
    internal static bool TryFindCycle(IEnumerable<string> roots, Func<string, IEnumerable<string>> childrenOf,
        out string cycleAt)
    {
        var visiting = new HashSet<string>(StringComparer.Ordinal);
        var settled = new HashSet<string>(StringComparer.Ordinal);

        bool Visit(string id)
        {
            if (visiting.Contains(id)) return true;
            if (!settled.Add(id)) return false;
            visiting.Add(id);
            foreach (string child in childrenOf(id))
                if (Visit(child)) return true;
            visiting.Remove(id);
            return false;
        }

        foreach (string root in roots)
            if (Visit(root))
            {
                cycleAt = root;
                return true;
            }

        cycleAt = string.Empty;
        return false;
    }
}

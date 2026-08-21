using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace Polaris.AI.Authoring;

public static partial class PaiValidator
{
    /// <summary>树之间的 SubTree 引用：tree 端口必须是字符串、必须指向存在的树，且引用不能成环。</summary>
    static void ValidateSubTrees(Dictionary<string, PaiTree> trees, List<PaiDiagnostic> result)
    {
        var references = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (PaiTree tree in trees.Values)
        {
            var targets = new List<string>();
            references[tree.Id] = targets;
            foreach (PaiNode node in tree.Nodes.Where(x => x.Type == "SubTree"))
            {
                if (!node.Ports.TryGetValue("tree", out JsonElement value) || value.ValueKind != JsonValueKind.String)
                {
                    result.Add(Error("PAI0301", "SubTree.tree must be a string.", tree.Id, node.Id));
                    continue;
                }
                string? target = value.GetString();
                if (string.IsNullOrEmpty(target) || !trees.ContainsKey(target!))
                    result.Add(Error("PAI0302", $"Referenced tree '{target}' does not exist.", tree.Id, node.Id));
                else targets.Add(target!);
            }
        }

        // 只有确认存在的树才会进 targets，所以每个出边目标一定也是 references 的 key。
        if (PaiGraph.TryFindCycle(references.Keys, id => references[id], out string cycleAt))
            result.Add(Error("PAI0303", "SubTree references are recursive.", cycleAt));
    }
}

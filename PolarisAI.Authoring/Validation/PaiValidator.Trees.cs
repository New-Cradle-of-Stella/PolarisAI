using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace Polaris.AI.Authoring;

public static partial class PaiValidator
{
    /// <summary>树表层面的校验：树 id 唯一、节点 id 全局唯一、节点总数上限、mainTree 存在。</summary>
    /// <returns>id 到树的映射，只含 id 合法且未重复的树；后续的 SubTree 校验以此为准。</returns>
    static Dictionary<string, PaiTree> ValidateTrees(PaiDocument document, PaiNodeCatalog? catalog,
        List<PaiDiagnostic> result)
    {
        var trees = new Dictionary<string, PaiTree>(StringComparer.Ordinal);
        var globalNodeIds = new HashSet<string>(StringComparer.Ordinal);
        int nodeCount = 0;
        foreach (PaiTree tree in document.Trees)
        {
            if (string.IsNullOrWhiteSpace(tree.Id))
            {
                result.Add(Error("PAI0101", "Tree id is required."));
                continue;
            }
            if (trees.ContainsKey(tree.Id)) result.Add(Error("PAI0102", $"Duplicate tree id '{tree.Id}'.", tree.Id));
            else trees.Add(tree.Id, tree);

            nodeCount += tree.Nodes.Count;
            foreach (PaiNode node in tree.Nodes)
                if (!string.IsNullOrWhiteSpace(node.Id) && !globalNodeIds.Add(node.Id))
                    result.Add(Error("PAI0010", $"Node id '{node.Id}' is duplicated across trees.", tree.Id, node.Id));

            ValidateTree(tree, catalog, result);
        }

        if (nodeCount > MaxNodes) result.Add(Error("PAI0008", $"At most {MaxNodes} nodes are allowed."));
        if (!trees.ContainsKey(document.MainTree)) result.Add(Error("PAI0009", $"mainTree '{document.MainTree}' does not exist."));
        return trees;
    }

    /// <summary>单棵树的节点图完整性：根存在且无父、每个节点可达、父子边合法、无环，以及节点是否符合目录描述。</summary>
    static void ValidateTree(PaiTree tree, PaiNodeCatalog? catalog, List<PaiDiagnostic> result)
    {
        Dictionary<string, PaiNode> nodes = CollectNodes(tree, result);
        if (string.IsNullOrWhiteSpace(tree.Root) || !nodes.ContainsKey(tree.Root))
            result.Add(Error("PAI0204", $"Root node '{tree.Root}' does not exist.", tree.Id));

        var parents = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (PaiNode node in nodes.Values)
        {
            PaiNodeDescriptor? descriptor = ResolveDescriptor(tree, node, catalog, result);
            if (descriptor != null) ValidateAgainstDescriptor(tree, node, descriptor, result);
            ValidateChildren(tree, node, nodes, parents, result);
        }

        if (nodes.ContainsKey(tree.Root) && parents.ContainsKey(tree.Root))
            result.Add(Error("PAI0213", "Root node cannot have a parent.", tree.Id, tree.Root));
        foreach (string id in nodes.Keys)
            if (id != tree.Root && !parents.ContainsKey(id))
                result.Add(Error("PAI0214", $"Node '{id}' is not reachable from the root.", tree.Id, id));

        if (PaiGraph.TryFindCycle(nodes.Keys, id => ChildrenOf(nodes, id), out string cycleAt))
            result.Add(Error("PAI0215", "The tree contains a cycle.", tree.Id, cycleAt));
    }

    /// <summary>收集节点并检查 id/type 的必填与唯一；id 空白或重复的节点不进入返回的表。</summary>
    static Dictionary<string, PaiNode> CollectNodes(PaiTree tree, List<PaiDiagnostic> result)
    {
        var nodes = new Dictionary<string, PaiNode>(StringComparer.Ordinal);
        foreach (PaiNode node in tree.Nodes)
        {
            if (string.IsNullOrWhiteSpace(node.Id))
            {
                result.Add(Error("PAI0201", "Node id is required.", tree.Id));
                continue;
            }
            if (nodes.ContainsKey(node.Id)) result.Add(Error("PAI0202", $"Duplicate node id '{node.Id}'.", tree.Id, node.Id));
            else nodes.Add(node.Id, node);
            if (string.IsNullOrWhiteSpace(node.Type)) result.Add(Error("PAI0203", "Node type is required.", tree.Id, node.Id));
        }
        return nodes;
    }

    /// <summary>没有目录时不做类型检查（外部工具可以只校验结构）；有目录但类型不认识才报错。</summary>
    static PaiNodeDescriptor? ResolveDescriptor(PaiTree tree, PaiNode node, PaiNodeCatalog? catalog,
        List<PaiDiagnostic> result)
    {
        if (catalog == null) return null;
        if (catalog.TryGet(node.Type, out PaiNodeDescriptor descriptor)) return descriptor;
        result.Add(Error("PAI0205", $"Unknown node type '{node.Type}'.", tree.Id, node.Id));
        return null;
    }

    /// <summary>节点与目录描述的一致性：子节点个数是否符合种类，端口是否齐全、认识、类型对得上。</summary>
    static void ValidateAgainstDescriptor(PaiTree tree, PaiNode node, PaiNodeDescriptor descriptor,
        List<PaiDiagnostic> result)
    {
        bool leaf = descriptor.Kind == PaiNodeKind.Action || descriptor.Kind == PaiNodeKind.Condition ||
                    descriptor.Kind == PaiNodeKind.SubTree;
        if (leaf && node.Children.Count != 0)
            result.Add(Error("PAI0206", $"{descriptor.Kind} node cannot have children.", tree.Id, node.Id));
        if (descriptor.Kind == PaiNodeKind.Decorator && node.Children.Count != 1)
            result.Add(Error("PAI0207", "Decorator node must have exactly one child.", tree.Id, node.Id));

        foreach (PaiPortDescriptor port in descriptor.Ports)
            if (port.Required && !node.Ports.ContainsKey(port.Name))
                result.Add(Error("PAI0208", $"Required port '{port.Name}' is missing.", tree.Id, node.Id));
        foreach (string port in node.Ports.Keys)
            if (!descriptor.Ports.Any(x => x.Name == port))
                result.Add(Error("PAI0209", $"Unknown port '{port}'.", tree.Id, node.Id));
        foreach (PaiPortDescriptor port in descriptor.Ports)
            if (node.Ports.TryGetValue(port.Name, out JsonElement value) && !PortMatches(port.Type, value))
                result.Add(Error("PAI0216", $"Port '{port.Name}' must be {port.Type} or a behavior binding.", tree.Id, node.Id));
    }

    /// <summary>父子边：子节点必须存在、不能重复、不能有第二个父节点。顺带把父子关系记进 <paramref name="parents"/>。</summary>
    static void ValidateChildren(PaiTree tree, PaiNode node, Dictionary<string, PaiNode> nodes,
        Dictionary<string, string> parents, List<PaiDiagnostic> result)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (string child in node.Children)
        {
            if (!nodes.ContainsKey(child)) result.Add(Error("PAI0210", $"Child node '{child}' does not exist.", tree.Id, node.Id));
            if (!seen.Add(child)) result.Add(Error("PAI0211", $"Child node '{child}' is repeated.", tree.Id, node.Id));
            if (parents.TryGetValue(child, out string existing))
                result.Add(Error("PAI0212", $"Node '{child}' has multiple parents ('{existing}' and '{node.Id}').", tree.Id, child));
            else parents[child] = node.Id;
        }
    }

    /// <summary>查环时的出边；引用了不存在的子节点已由 PAI0210 报过，这里当作没有出边。</summary>
    static IEnumerable<string> ChildrenOf(Dictionary<string, PaiNode> nodes, string id)
        => nodes.TryGetValue(id, out PaiNode node) ? node.Children : Enumerable.Empty<string>();
}

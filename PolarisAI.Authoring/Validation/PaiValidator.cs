using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace Polaris.AI.Authoring;

public enum PaiDiagnosticSeverity
{
    Warning,
    Error,
}

public sealed class PaiDiagnostic
{
    public PaiDiagnostic(string code, PaiDiagnosticSeverity severity, string message,
        string? treeId = null, string? nodeId = null)
    {
        Code = code;
        Severity = severity;
        Message = message;
        TreeId = treeId;
        NodeId = nodeId;
    }

    public string Code { get; }
    public PaiDiagnosticSeverity Severity { get; }
    public string Message { get; }
    public string? TreeId { get; }
    public string? NodeId { get; }
    public override string ToString() => $"{Code}: {Message}";
}

public static class PaiValidator
{
    public const int MaxTrees = 256;
    public const int MaxNodes = 10000;
    public const int MaxImports = 64;

    public static IReadOnlyList<PaiDiagnostic> Validate(PaiDocument? document, PaiNodeCatalog? catalog = null)
    {
        var result = new List<PaiDiagnostic>();
        if (document == null)
        {
            result.Add(Error("PAI0001", "The .pai document is null."));
            return result;
        }

        if (!string.Equals(document.Format, PaiDocument.FormatName, StringComparison.Ordinal))
            result.Add(Error("PAI0002", $"format must be '{PaiDocument.FormatName}'."));
        if (document.Version != PaiDocument.CurrentVersion)
            result.Add(Error("PAI0003", $"Unsupported .pai version {document.Version}."));
        if (string.IsNullOrWhiteSpace(document.Id)) result.Add(Error("PAI0004", "Behavior id is required."));
        if (string.IsNullOrWhiteSpace(document.MainTree)) result.Add(Error("PAI0005", "mainTree is required."));
        if (document.Imports.Count > MaxImports) result.Add(Error("PAI0006", $"At most {MaxImports} imports are allowed."));
        if (document.Trees.Count > MaxTrees) result.Add(Error("PAI0007", $"At most {MaxTrees} trees are allowed."));

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

        ValidateSubTrees(trees, result);
        ValidateAttributes(document.BehaviorAttributes, result);
        ValidateEditor(document, result);
        return result;
    }

    public static bool HasErrors(IEnumerable<PaiDiagnostic> diagnostics)
        => diagnostics.Any(x => x.Severity == PaiDiagnosticSeverity.Error);

    static void ValidateTree(PaiTree tree, PaiNodeCatalog? catalog, List<PaiDiagnostic> result)
    {
        var nodes = new Dictionary<string, PaiNode>(StringComparer.Ordinal);
        var parent = new Dictionary<string, string>(StringComparer.Ordinal);
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
        if (string.IsNullOrWhiteSpace(tree.Root) || !nodes.ContainsKey(tree.Root))
            result.Add(Error("PAI0204", $"Root node '{tree.Root}' does not exist.", tree.Id));

        foreach (PaiNode node in nodes.Values)
        {
            PaiNodeDescriptor? descriptor = null;
            if (catalog != null && !catalog.TryGet(node.Type, out descriptor!))
                result.Add(Error("PAI0205", $"Unknown node type '{node.Type}'.", tree.Id, node.Id));

            if (descriptor != null)
            {
                if ((descriptor.Kind == PaiNodeKind.Action || descriptor.Kind == PaiNodeKind.Condition || descriptor.Kind == PaiNodeKind.SubTree) && node.Children.Count != 0)
                    result.Add(Error("PAI0206", $"{descriptor.Kind} node cannot have children.", tree.Id, node.Id));
                if (descriptor.Kind == PaiNodeKind.Decorator && node.Children.Count != 1)
                    result.Add(Error("PAI0207", "Decorator node must have exactly one child.", tree.Id, node.Id));
                foreach (PaiPortDescriptor port in descriptor.Ports.Where(x => x.Required))
                    if (!node.Ports.ContainsKey(port.Name)) result.Add(Error("PAI0208", $"Required port '{port.Name}' is missing.", tree.Id, node.Id));
                foreach (string port in node.Ports.Keys)
                    if (!descriptor.Ports.Any(x => x.Name == port)) result.Add(Error("PAI0209", $"Unknown port '{port}'.", tree.Id, node.Id));
                foreach (PaiPortDescriptor port in descriptor.Ports)
                    if (node.Ports.TryGetValue(port.Name, out JsonElement value) && !PortMatches(port.Type, value))
                        result.Add(Error("PAI0216", $"Port '{port.Name}' must be {port.Type} or a behavior binding.", tree.Id, node.Id));
            }

            var seenChildren = new HashSet<string>(StringComparer.Ordinal);
            foreach (string child in node.Children)
            {
                if (!nodes.ContainsKey(child)) result.Add(Error("PAI0210", $"Child node '{child}' does not exist.", tree.Id, node.Id));
                if (!seenChildren.Add(child)) result.Add(Error("PAI0211", $"Child node '{child}' is repeated.", tree.Id, node.Id));
                if (parent.TryGetValue(child, out string existing))
                    result.Add(Error("PAI0212", $"Node '{child}' has multiple parents ('{existing}' and '{node.Id}').", tree.Id, child));
                else parent[child] = node.Id;
            }
        }

        if (nodes.ContainsKey(tree.Root) && parent.ContainsKey(tree.Root)) result.Add(Error("PAI0213", "Root node cannot have a parent.", tree.Id, tree.Root));
        foreach (string id in nodes.Keys)
            if (id != tree.Root && !parent.ContainsKey(id)) result.Add(Error("PAI0214", $"Node '{id}' is not reachable from the root.", tree.Id, id));
        DetectCycles(tree, nodes, result);
    }

    static void DetectCycles(PaiTree tree, Dictionary<string, PaiNode> nodes, List<PaiDiagnostic> result)
    {
        var visiting = new HashSet<string>(StringComparer.Ordinal);
        var visited = new HashSet<string>(StringComparer.Ordinal);
        bool Visit(string id)
        {
            if (visiting.Contains(id)) return true;
            if (!nodes.TryGetValue(id, out PaiNode node) || visited.Contains(id)) return false;
            visiting.Add(id);
            foreach (string child in node.Children)
                if (Visit(child)) return true;
            visiting.Remove(id);
            visited.Add(id);
            return false;
        }
        foreach (string id in nodes.Keys)
            if (Visit(id))
            {
                result.Add(Error("PAI0215", "The tree contains a cycle.", tree.Id, id));
                return;
            }
    }

    static void ValidateSubTrees(Dictionary<string, PaiTree> trees, List<PaiDiagnostic> result)
    {
        var edges = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (PaiTree tree in trees.Values)
        {
            var refs = new List<string>();
            edges[tree.Id] = refs;
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
                else refs.Add(target!);
            }
        }
        var visiting = new HashSet<string>(StringComparer.Ordinal);
        var visited = new HashSet<string>(StringComparer.Ordinal);
        bool Visit(string id)
        {
            if (visiting.Contains(id)) return true;
            if (visited.Contains(id)) return false;
            visiting.Add(id);
            foreach (string next in edges[id]) if (Visit(next)) return true;
            visiting.Remove(id);
            visited.Add(id);
            return false;
        }
        foreach (string id in edges.Keys)
            if (Visit(id))
            {
                result.Add(Error("PAI0303", "SubTree references are recursive.", id));
                break;
            }
    }

    static void ValidateAttributes(Dictionary<string, PaiBehaviorAttribute> attributes, List<PaiDiagnostic> result)
    {
        foreach (KeyValuePair<string, PaiBehaviorAttribute> pair in attributes)
        {
            if (string.IsNullOrWhiteSpace(pair.Key)) result.Add(Error("PAI0401", "Behavior attribute key is required."));
            PaiBehaviorAttribute? attribute = pair.Value;
            string type = attribute?.Type ?? string.Empty;
            if (type != "string" && type != "boolean" && type != "integer" && type != "number")
                result.Add(Error("PAI0402", $"Behavior attribute '{pair.Key}' has unsupported type '{type}'."));
            else if (attribute == null || attribute.Default.ValueKind == JsonValueKind.Undefined || !ScalarMatches(type, attribute.Default))
                result.Add(Error("PAI0403", $"Behavior attribute '{pair.Key}' default does not match type '{type}'."));
        }
    }

    static void ValidateEditor(PaiDocument document, List<PaiDiagnostic> result)
    {
        var known = new HashSet<string>(document.Trees.SelectMany(x => x.Nodes).Select(x => x.Id), StringComparer.Ordinal);
        foreach (KeyValuePair<string, PaiNodeLayout> layout in document.Editor.Nodes)
            if (!known.Contains(layout.Key)) result.Add(new PaiDiagnostic("PAI0901", PaiDiagnosticSeverity.Warning,
                $"Editor layout references unknown node '{layout.Key}'.", nodeId: layout.Key));
        if (double.IsNaN(document.Editor.Viewport.Zoom) || double.IsInfinity(document.Editor.Viewport.Zoom) || document.Editor.Viewport.Zoom <= 0)
            result.Add(new PaiDiagnostic("PAI0902", PaiDiagnosticSeverity.Warning, "Editor zoom is invalid and will be ignored."));
    }

    static PaiDiagnostic Error(string code, string message, string? tree = null, string? node = null)
        => new PaiDiagnostic(code, PaiDiagnosticSeverity.Error, message, tree, node);

    static bool PortMatches(string type, JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.String)
        {
            string? text = value.GetString();
            if (text != null && text.StartsWith("{behavior.", StringComparison.Ordinal) && text.EndsWith("}", StringComparison.Ordinal))
                return text.Length > "{behavior.}".Length;
        }
        return ScalarMatches(type, value);
    }

    static bool ScalarMatches(string type, JsonElement value)
        => type switch
        {
            "string" => value.ValueKind == JsonValueKind.String,
            "boolean" => value.ValueKind == JsonValueKind.True || value.ValueKind == JsonValueKind.False,
            "integer" => value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out _),
            "number" => value.ValueKind == JsonValueKind.Number,
            _ => true,
        };
}

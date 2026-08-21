using System;
using System.Collections.Generic;
using System.Linq;

namespace Polaris.AI.Authoring;

/// <summary>
/// .pai 文档校验的入口。实现按关注点分到同一个 partial 类的多个文件里：本文件是入口与文档级 schema，
/// <c>PaiValidator.Trees.cs</c> 管树表与节点图的完整性，<c>PaiValidator.SubTrees.cs</c> 管树间引用递归，
/// <c>PaiValidator.Attributes.cs</c> 管行为属性与端口取值的类型一致性，<c>PaiValidator.Editor.cs</c> 管编辑器元数据。
/// </summary>
public static partial class PaiValidator
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

        ValidateSchema(document, result);
        Dictionary<string, PaiTree> trees = ValidateTrees(document, catalog, result);
        ValidateSubTrees(trees, result);
        ValidateAttributes(document.BehaviorAttributes, result);
        ValidateEditor(document, result);
        return result;
    }

    public static bool HasErrors(IEnumerable<PaiDiagnostic> diagnostics)
        => diagnostics.Any(x => x.Severity == PaiDiagnosticSeverity.Error);

    /// <summary>文档头部与规模上限：格式名、版本、必填 id/mainTree、导入与树的数量。</summary>
    static void ValidateSchema(PaiDocument document, List<PaiDiagnostic> result)
    {
        if (!string.Equals(document.Format, PaiDocument.FormatName, StringComparison.Ordinal))
            result.Add(Error("PAI0002", $"format must be '{PaiDocument.FormatName}'."));
        if (document.Version != PaiDocument.CurrentVersion)
            result.Add(Error("PAI0003", $"Unsupported .pai version {document.Version}."));
        if (string.IsNullOrWhiteSpace(document.Id)) result.Add(Error("PAI0004", "Behavior id is required."));
        if (string.IsNullOrWhiteSpace(document.MainTree)) result.Add(Error("PAI0005", "mainTree is required."));
        if (document.Imports.Count > MaxImports) result.Add(Error("PAI0006", $"At most {MaxImports} imports are allowed."));
        if (document.Trees.Count > MaxTrees) result.Add(Error("PAI0007", $"At most {MaxTrees} trees are allowed."));
    }

    static PaiDiagnostic Error(string code, string message, string? tree = null, string? node = null)
        => new PaiDiagnostic(code, PaiDiagnosticSeverity.Error, message, tree, node);
}

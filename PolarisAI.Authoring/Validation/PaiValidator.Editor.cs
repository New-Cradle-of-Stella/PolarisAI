using System;
using System.Collections.Generic;
using System.Linq;

namespace Polaris.AI.Authoring;

public static partial class PaiValidator
{
    /// <summary>
    /// 编辑器元数据只影响画布显示，不影响运行时，所以这里一律只给 Warning：
    /// 布局引用了不存在的节点、缩放值非法都只是"会被忽略"。
    /// </summary>
    static void ValidateEditor(PaiDocument document, List<PaiDiagnostic> result)
    {
        var known = new HashSet<string>(document.Trees.SelectMany(x => x.Nodes).Select(x => x.Id), StringComparer.Ordinal);
        foreach (KeyValuePair<string, PaiNodeLayout> layout in document.Editor.Nodes)
            if (!known.Contains(layout.Key))
                result.Add(new PaiDiagnostic("PAI0901", PaiDiagnosticSeverity.Warning,
                    $"Editor layout references unknown node '{layout.Key}'.", nodeId: layout.Key));

        double zoom = document.Editor.Viewport.Zoom;
        if (double.IsNaN(zoom) || double.IsInfinity(zoom) || zoom <= 0)
            result.Add(new PaiDiagnostic("PAI0902", PaiDiagnosticSeverity.Warning, "Editor zoom is invalid and will be ignored."));
    }
}

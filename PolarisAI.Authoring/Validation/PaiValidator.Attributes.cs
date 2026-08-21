using System;
using System.Collections.Generic;
using System.Text.Json;

namespace Polaris.AI.Authoring;

public static partial class PaiValidator
{
    /// <summary>行为属性表：key 必填，type 必须是支持的四种标量之一，default 必须与 type 对得上。</summary>
    static void ValidateAttributes(Dictionary<string, PaiBehaviorAttribute> attributes, List<PaiDiagnostic> result)
    {
        foreach (KeyValuePair<string, PaiBehaviorAttribute> pair in attributes)
        {
            if (string.IsNullOrWhiteSpace(pair.Key)) result.Add(Error("PAI0401", "Behavior attribute key is required."));
            PaiBehaviorAttribute? attribute = pair.Value;
            string type = attribute?.Type ?? string.Empty;
            if (type != "string" && type != "boolean" && type != "integer" && type != "number")
                result.Add(Error("PAI0402", $"Behavior attribute '{pair.Key}' has unsupported type '{type}'."));
            else if (attribute == null || attribute.Default.ValueKind == JsonValueKind.Undefined ||
                     !ScalarMatches(type, attribute.Default))
                result.Add(Error("PAI0403", $"Behavior attribute '{pair.Key}' default does not match type '{type}'."));
        }
    }

    /// <summary>端口取值：要么是声明类型的标量，要么是 <c>{behavior.xxx}</c> 形式的行为属性绑定。</summary>
    static bool PortMatches(string type, JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.String)
        {
            string? text = value.GetString();
            if (text != null && text.StartsWith("{behavior.", StringComparison.Ordinal) &&
                text.EndsWith("}", StringComparison.Ordinal))
                return text.Length > "{behavior.}".Length;
        }
        return ScalarMatches(type, value);
    }

    /// <summary>JSON 标量与 .pai 声明类型是否一致；类型名不认识时不在这里报错（由 PAI0402 负责）。</summary>
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

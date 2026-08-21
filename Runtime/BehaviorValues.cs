using System;
using System.Text.Json;
using Polaris.API;

namespace Polaris.AI;

internal static class BehaviorValues
{
    internal static bool IsSupported(object? value)
        => value == null || value is string || value is bool || value is byte || value is sbyte ||
           value is short || value is ushort || value is int || value is uint || value is long || value is ulong ||
           value is float || value is double || value is decimal || value is Enum || value is GameVector2;

    internal static object? FromJson(JsonElement value)
        => value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Number when value.TryGetInt64(out long integer) => integer >= int.MinValue && integer <= int.MaxValue ? (object)(int)integer : integer,
            JsonValueKind.Number => value.GetDouble(),
            JsonValueKind.Null => null,
            _ => throw new InvalidOperationException("Behavior attributes must be scalar values."),
        };

    internal static bool Matches(string type, object? value)
        => type switch
        {
            "string" => value is string,
            "boolean" => value is bool,
            "integer" => value is byte || value is sbyte || value is short || value is ushort || value is int ||
                         value is uint || value is long || value is ulong,
            "number" => value is byte || value is sbyte || value is short || value is ushort || value is int ||
                        value is uint || value is long || value is ulong || value is float || value is double || value is decimal,
            _ => false,
        };
}

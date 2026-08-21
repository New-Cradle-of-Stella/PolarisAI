using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Polaris.AI.Authoring;

public sealed class PaiDocument
{
    public const string FormatName = "polaris.ai.behavior";
    public const int CurrentVersion = 1;

    [JsonPropertyName("format")]
    public string Format { get; set; } = FormatName;

    [JsonPropertyName("version")]
    public int Version { get; set; } = CurrentVersion;

    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("mainTree")]
    public string MainTree { get; set; } = "main";

    [JsonPropertyName("imports")]
    public List<string> Imports { get; set; } = new List<string>();

    [JsonPropertyName("behaviorAttributes")]
    public Dictionary<string, PaiBehaviorAttribute> BehaviorAttributes { get; set; } =
        new Dictionary<string, PaiBehaviorAttribute>(StringComparer.Ordinal);

    [JsonPropertyName("trees")]
    public List<PaiTree> Trees { get; set; } = new List<PaiTree>();

    [JsonPropertyName("editor")]
    public PaiEditorState Editor { get; set; } = new PaiEditorState();

    [JsonPropertyName("extensions")]
    public Dictionary<string, JsonElement> Extensions { get; set; } =
        new Dictionary<string, JsonElement>(StringComparer.Ordinal);
}

public sealed class PaiBehaviorAttribute
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "string";

    [JsonPropertyName("default")]
    public JsonElement Default { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }
}

public sealed class PaiTree
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("root")]
    public string Root { get; set; } = string.Empty;

    [JsonPropertyName("nodes")]
    public List<PaiNode> Nodes { get; set; } = new List<PaiNode>();
}

public sealed class PaiNode
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("ports")]
    public Dictionary<string, JsonElement> Ports { get; set; } =
        new Dictionary<string, JsonElement>(StringComparer.Ordinal);

    [JsonPropertyName("children")]
    public List<string> Children { get; set; } = new List<string>();
}

public sealed class PaiEditorState
{
    [JsonPropertyName("viewport")]
    public PaiViewport Viewport { get; set; } = new PaiViewport();

    [JsonPropertyName("nodes")]
    public Dictionary<string, PaiNodeLayout> Nodes { get; set; } =
        new Dictionary<string, PaiNodeLayout>(StringComparer.Ordinal);
}

public sealed class PaiViewport
{
    [JsonPropertyName("x")]
    public double X { get; set; }

    [JsonPropertyName("y")]
    public double Y { get; set; }

    [JsonPropertyName("zoom")]
    public double Zoom { get; set; } = 1d;
}

public sealed class PaiNodeLayout
{
    [JsonPropertyName("x")]
    public double X { get; set; }

    [JsonPropertyName("y")]
    public double Y { get; set; }

    [JsonPropertyName("collapsed")]
    public bool Collapsed { get; set; }
}

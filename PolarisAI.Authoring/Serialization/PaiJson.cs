using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Polaris.AI.Authoring;

public static class PaiJson
{
    static readonly JsonSerializerOptions ReadOptions = CreateOptions(false);
    static readonly JsonSerializerOptions WriteOptions = CreateOptions(true);

    public static PaiDocument Parse(string json)
    {
        if (json == null) throw new ArgumentNullException(nameof(json));
        PaiDocument? document = JsonSerializer.Deserialize<PaiDocument>(json, ReadOptions);
        if (document == null) throw new JsonException("The .pai document is empty.");
        Normalize(document);
        return document;
    }

    public static PaiDocument Load(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Path cannot be empty.", nameof(path));
        return Parse(File.ReadAllText(path, Encoding.UTF8));
    }

    public static string Serialize(PaiDocument document)
    {
        if (document == null) throw new ArgumentNullException(nameof(document));
        Normalize(document);
        return JsonSerializer.Serialize(document, WriteOptions) + Environment.NewLine;
    }

    public static void Save(string path, PaiDocument document)
    {
        if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Path cannot be empty.", nameof(path));
        string fullPath = Path.GetFullPath(path);
        string? directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

        string temporary = fullPath + ".tmp";
        File.WriteAllText(temporary, Serialize(document), new UTF8Encoding(false));
        if (File.Exists(fullPath))
        {
            string backup = fullPath + ".bak";
            File.Replace(temporary, fullPath, backup, true);
        }
        else
        {
            File.Move(temporary, fullPath);
        }
    }

    static JsonSerializerOptions CreateOptions(bool indented)
        => new JsonSerializerOptions
        {
            AllowTrailingCommas = false,
            ReadCommentHandling = JsonCommentHandling.Disallow,
            PropertyNameCaseInsensitive = false,
            WriteIndented = indented,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
            MaxDepth = 128,
        };

    static void Normalize(PaiDocument document)
    {
        document.Imports ??= new List<string>();
        document.BehaviorAttributes ??= new Dictionary<string, PaiBehaviorAttribute>(StringComparer.Ordinal);
        document.Trees ??= new List<PaiTree>();
        document.Editor ??= new PaiEditorState();
        document.Extensions ??= new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        document.Editor.Nodes ??= new Dictionary<string, PaiNodeLayout>(StringComparer.Ordinal);
        document.Editor.Viewport ??= new PaiViewport();
        foreach (PaiTree tree in document.Trees)
        {
            tree.Nodes ??= new List<PaiNode>();
            foreach (PaiNode node in tree.Nodes)
            {
                node.Ports ??= new Dictionary<string, JsonElement>(StringComparer.Ordinal);
                node.Children ??= new List<string>();
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace Polaris.AI.Authoring;

public sealed class PnpcDocument
{
    public const int CurrentVersion = 1;

    public int Version { get; set; } = CurrentVersion;
    public string Id { get; set; } = string.Empty;
    public string CharacterResource { get; set; } = string.Empty;
    public string InitialPose { get; set; } = "stand";
    public float Width { get; set; } = 0.5f;
    public float Height { get; set; } = 1f;
    public int MaxHp { get; set; } = 100;
    public int MaxMp { get; set; } = 100;
    public string HitType { get; set; } = "None";
    public string? Faction { get; set; }
    public string? DefaultBehavior { get; set; }
}

public sealed class PnpcDiagnostic
{
    public PnpcDiagnostic(string code, string message) { Code = code; Message = message; }
    public string Code { get; }
    public string Message { get; }
    public override string ToString() => $"{Code}: {Message}";
}

public static class PnpcXml
{
    static readonly Regex IdPattern = new Regex(@"^[A-Za-z][A-Za-z0-9_.:-]*$", RegexOptions.Compiled);
    static readonly Regex ResourcePattern = new Regex(@"^[A-Za-z_]\w*(?:\.[A-Za-z_]\w*)+$", RegexOptions.Compiled);
    static readonly HashSet<string> Attributes = new HashSet<string>(StringComparer.Ordinal)
    {
        "Version", "Id", "CharacterResource", "InitialPose", "Width", "Height", "MaxHp", "MaxMp",
        "HitType", "Faction", "DefaultBehavior",
    };

    public static PnpcDocument Parse(string xml)
    {
        if (xml == null) throw new ArgumentNullException(nameof(xml));
        XDocument source;
        try { source = XDocument.Parse(xml, LoadOptions.SetLineInfo); }
        catch (XmlException ex) { throw new FormatException($"Invalid .pnpc XML: {ex.Message}", ex); }

        XElement root = source.Root ?? throw new FormatException("The .pnpc document is empty.");
        if (root.Name.NamespaceName.Length != 0 || root.Name.LocalName != "PNpc")
            throw new FormatException("The .pnpc root element must be <PNpc> without a namespace.");
        if (root.Elements().Any()) throw new FormatException("<PNpc> cannot contain child elements in version 1.");
        foreach (XAttribute attribute in root.Attributes())
            if (!attribute.IsNamespaceDeclaration && !Attributes.Contains(attribute.Name.LocalName))
                throw new FormatException($"Unknown .pnpc attribute '{attribute.Name.LocalName}'.");

        var document = new PnpcDocument
        {
            Version = Integer(root, "Version", PnpcDocument.CurrentVersion),
            Id = Text(root, "Id"),
            CharacterResource = Text(root, "CharacterResource"),
            InitialPose = Text(root, "InitialPose", "stand"),
            Width = Number(root, "Width", 0.5f),
            Height = Number(root, "Height", 1f),
            MaxHp = Integer(root, "MaxHp", 100),
            MaxMp = Integer(root, "MaxMp", 100),
            HitType = Text(root, "HitType", "None"),
            Faction = Optional(root, "Faction"),
            DefaultBehavior = Optional(root, "DefaultBehavior"),
        };
        return document;
    }

    public static PnpcDocument Load(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Path cannot be empty.", nameof(path));
        return Parse(File.ReadAllText(path, Encoding.UTF8));
    }

    public static IReadOnlyList<PnpcDiagnostic> Validate(PnpcDocument? document)
    {
        var result = new List<PnpcDiagnostic>();
        if (document == null) { result.Add(new PnpcDiagnostic("PNPC0001", "The document is null.")); return result; }
        if (document.Version != PnpcDocument.CurrentVersion) result.Add(new PnpcDiagnostic("PNPC0002", $"Unsupported version {document.Version}."));
        if (!IdPattern.IsMatch(document.Id ?? string.Empty)) result.Add(new PnpcDiagnostic("PNPC0003", "Id must be a stable ASCII identifier."));
        if (!ResourcePattern.IsMatch(document.CharacterResource ?? string.Empty)) result.Add(new PnpcDiagnostic("PNPC0004", "CharacterResource must be a qualified static field reference."));
        if (string.IsNullOrWhiteSpace(document.InitialPose)) result.Add(new PnpcDiagnostic("PNPC0005", "InitialPose is required."));
        if (!FinitePositive(document.Width) || !FinitePositive(document.Height)) result.Add(new PnpcDiagnostic("PNPC0006", "Width and Height must be finite positive numbers."));
        if (document.MaxHp <= 0 || document.MaxMp < 0) result.Add(new PnpcDiagnostic("PNPC0007", "MaxHp must be positive and MaxMp cannot be negative."));
        if (document.HitType != "None" && document.HitType != "Player" && document.HitType != "Enemy")
            result.Add(new PnpcDiagnostic("PNPC0008", "HitType must be None, Player, or Enemy."));
        if (document.Faction != null && string.IsNullOrWhiteSpace(document.Faction)) result.Add(new PnpcDiagnostic("PNPC0009", "Faction cannot be blank."));
        if (document.DefaultBehavior != null && string.IsNullOrWhiteSpace(document.DefaultBehavior)) result.Add(new PnpcDiagnostic("PNPC0010", "DefaultBehavior cannot be blank."));
        return result;
    }

    public static string Serialize(PnpcDocument document)
    {
        if (document == null) throw new ArgumentNullException(nameof(document));
        var root = new XElement("PNpc",
            new XAttribute("Version", document.Version),
            new XAttribute("Id", document.Id),
            new XAttribute("CharacterResource", document.CharacterResource),
            new XAttribute("InitialPose", document.InitialPose),
            new XAttribute("Width", document.Width.ToString("0.###", CultureInfo.InvariantCulture)),
            new XAttribute("Height", document.Height.ToString("0.###", CultureInfo.InvariantCulture)),
            new XAttribute("MaxHp", document.MaxHp),
            new XAttribute("MaxMp", document.MaxMp),
            new XAttribute("HitType", document.HitType));
        if (document.Faction != null) root.Add(new XAttribute("Faction", document.Faction));
        if (document.DefaultBehavior != null) root.Add(new XAttribute("DefaultBehavior", document.DefaultBehavior));
        return new XDocument(new XDeclaration("1.0", "utf-8", null), root).ToString() + Environment.NewLine;
    }

    static bool FinitePositive(float value) => value > 0 && !float.IsNaN(value) && !float.IsInfinity(value);
    static string Text(XElement root, string name, string fallback = "") => (string?)root.Attribute(name) ?? fallback;
    static string? Optional(XElement root, string name) => root.Attribute(name) is XAttribute value ? value.Value : null;
    static int Integer(XElement root, string name, int fallback)
        => root.Attribute(name) is not XAttribute value ? fallback : int.TryParse(value.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)
            ? parsed : throw new FormatException($"{name} must be an integer.");
    static float Number(XElement root, string name, float fallback)
        => root.Attribute(name) is not XAttribute value ? fallback : float.TryParse(value.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out float parsed)
            ? parsed : throw new FormatException($"{name} must be a number.");
}

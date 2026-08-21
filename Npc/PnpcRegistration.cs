using System;
using System.Collections.Generic;
using Polaris.Res.Pxls;

namespace Polaris.AI.Registration;

public enum PnpcHitType
{
    None,
    Player,
    Enemy,
}

public sealed class PnpcDefinition
{
    public PnpcDefinition(string id, string initialPose, float width, float height, int maxHp, int maxMp,
        PnpcHitType hitType = PnpcHitType.None, string? faction = null, string? defaultBehavior = null)
    {
        if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("NPC id is required.", nameof(id));
        if (string.IsNullOrWhiteSpace(initialPose)) throw new ArgumentException("Initial pose is required.", nameof(initialPose));
        if (width <= 0 || height <= 0 || float.IsNaN(width) || float.IsNaN(height) || float.IsInfinity(width) || float.IsInfinity(height))
            throw new ArgumentOutOfRangeException(nameof(width), "NPC dimensions must be finite positive numbers.");
        if (maxHp <= 0 || maxMp < 0) throw new ArgumentOutOfRangeException(nameof(maxHp));
        if (!Enum.IsDefined(typeof(PnpcHitType), hitType)) throw new ArgumentOutOfRangeException(nameof(hitType));
        Id = id;
        InitialPose = initialPose;
        Width = width;
        Height = height;
        MaxHp = maxHp;
        MaxMp = maxMp;
        HitType = hitType;
        Faction = faction;
        DefaultBehavior = defaultBehavior;
    }

    public string Id { get; }
    public string InitialPose { get; }
    public float Width { get; }
    public float Height { get; }
    public int MaxHp { get; }
    public int MaxMp { get; }
    public PnpcHitType HitType { get; }
    public string? Faction { get; }
    public string? DefaultBehavior { get; }
}

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class PnpcAutoRegistrationAttribute : Attribute { }

public interface IPnpcRegistrar
{
    void Register(PnpcRegistrationContext context);
}

public sealed class PnpcRegistrationContext
{
    readonly List<PnpcSubmission> submissions = new List<PnpcSubmission>();
    internal PnpcRegistrationContext(string owner) { Owner = owner; }
    public string Owner { get; }

    public void Register(PnpcDefinition definition, Func<PxlsCharacterHandle> characterResource)
    {
        if (definition == null) throw new ArgumentNullException(nameof(definition));
        if (characterResource == null) throw new ArgumentNullException(nameof(characterResource));
        submissions.Add(new PnpcSubmission(definition, characterResource, Owner));
    }

    internal IReadOnlyList<PnpcSubmission> Submissions => submissions;
}

internal sealed class PnpcSubmission
{
    internal PnpcSubmission(PnpcDefinition definition, Func<PxlsCharacterHandle> resource, string owner)
    { Definition = definition; Resource = resource; Owner = owner; }
    internal PnpcDefinition Definition { get; }
    internal Func<PxlsCharacterHandle> Resource { get; }
    internal string Owner { get; }
}

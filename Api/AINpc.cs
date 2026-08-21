using System;
using Polaris.API;

namespace Polaris.AI;

public sealed class AINpc : AIActor
{
    readonly Action? despawn;
    readonly Action<bool>? setVisible;
    string? faction;
    bool visible = true;

    internal AINpc(GameCharacter character, GameMap map, string instanceKey, string? faction,
        Action? despawn, Action<bool>? setVisible)
        : base(character, map)
    {
        InstanceKey = instanceKey;
        this.faction = faction;
        this.despawn = despawn;
        this.setVisible = setVisible;
    }

    public string InstanceKey { get; }
    public string? Faction => IsValid ? faction : null;
    public bool IsVisible => IsValid && visible;

    public bool SetFaction(string faction)
    {
        EnsureUsable();
        if (string.IsNullOrWhiteSpace(faction)) return false;
        this.faction = faction;
        return true;
    }

    public void SetVisible(bool visible)
    {
        EnsureUsable();
        setVisible?.Invoke(visible);
        this.visible = visible;
    }

    public bool Despawn()
    {
        EnsureUsable();
        GameCharacter character = Character!;
        try
        {
            despawn?.Invoke();
            AIActorRegistry.RemoveNpc(this, character);
            Release(AbortReason.Despawned);
            return true;
        }
        catch (Exception ex)
        {
            PolarisAPI.Errors.Report(ex, $"PolarisAI despawn '{InstanceKey}'");
            return false;
        }
    }
}

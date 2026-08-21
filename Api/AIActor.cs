using System;
using System.Collections.Generic;
using Polaris.API;

namespace Polaris.AI;

public class AIActor
{
    readonly Dictionary<string, object?> behaviorAttributes = new Dictionary<string, object?>(StringComparer.Ordinal);
    GameCharacter? character;
    GameMap? map;
    GameCharacter? target;
    BehaviorRuntime? behavior;
    bool behaviorEnabled;
    bool globallySuspended;
    bool detached;

    internal AIActor(GameCharacter character, GameMap? map)
    {
        this.character = character ?? throw new ArgumentNullException(nameof(character));
        this.map = map;
    }

    public bool IsValid => !detached && character?.IsValid == true && (map == null || map.IsValid);
    public GameCharacter? Character => IsValid ? character : null;
    public GameMap? Map => IsValid ? map : null;
    public float X => Character?.X ?? 0f;
    public float Y => Character?.Y ?? 0f;
    public float VelocityX => Character?.VelocityX ?? 0f;
    public float VelocityY => Character?.VelocityY ?? 0f;
    public float Width => Character?.Width ?? 0f;
    public float Height => Character?.Height ?? 0f;
    public GameFacing Facing => Character?.Facing ?? GameFacing.Right;
    public int Hp => Character?.Hp ?? 0;
    public int MaxHp => Character?.MaxHp ?? 0;
    public int Mp => Character?.Mp ?? 0;
    public int MaxMp => Character?.MaxMp ?? 0;
    public bool IsAlive => Character?.IsAlive ?? false;
    public string? Key => Character?.Key;
    public string? BehaviorId => IsValid ? behavior?.BehaviorId : null;
    public bool IsBehaviorEnabled => IsValid && behavior != null && behaviorEnabled;
    public GameCharacter? Target => IsValid && target?.IsValid == true ? target : null;

    public void Teleport(GameVector2 position) { EnsureUsable(); character!.Teleport(position); }
    public bool MoveBy(GameVector2 delta, bool checkFoot = true) { EnsureUsable(); return character!.MoveBy(delta, checkFoot); }
    public void SetVelocity(GameVector2 velocity) { EnsureUsable(); character!.SetVelocity(velocity); }
    public void SetFacing(GameFacing facing, bool forceSprite = false) { EnsureUsable(); character!.SetFacing(facing, forceSprite); }
    public bool PoseIs(string pose) => IsValid && character!.PoseIs(pose);
    public bool SetPose(string pose) { EnsureUsable(); return character!.SetPose(pose); }
    public bool MoveToAnchor(string anchorKey) { EnsureUsable(); return character!.MoveToAnchor(anchorKey); }
    public void HealHp(int amount) { EnsureUsable(); character!.HealHp(amount); }
    public void HealMp(int amount) { EnsureUsable(); character!.HealMp(amount); }
    public int DamageHp(int amount, bool force = false) { EnsureUsable(); return character!.DamageHp(amount, force); }
    public int DamageMp(int amount, bool force = false) { EnsureUsable(); return character!.DamageMp(amount, force); }

    public bool AttachBehavior(string behaviorId, IReadOnlyDictionary<string, object?>? attributes = null)
    {
        EnsureUsable();
        if (string.IsNullOrWhiteSpace(behaviorId)) throw new ArgumentException("Behavior id cannot be empty.", nameof(behaviorId));
        if (!BehaviorRepository.TryCreate(behaviorId, this, attributes, out BehaviorRuntime? next)) return false;
        behavior?.Abort(AbortReason.Replaced);
        behavior = next!;
        behaviorAttributes.Clear();
        foreach (KeyValuePair<string, object?> value in behavior.Attributes) behaviorAttributes[value.Key] = value.Value;
        behaviorEnabled = true;
        globallySuspended = false;
        return true;
    }

    public bool DetachBehavior()
    {
        EnsureUsable();
        if (behavior == null) return false;
        behavior.Abort(AbortReason.Detached);
        behavior = null;
        behaviorAttributes.Clear();
        behaviorEnabled = false;
        return true;
    }

    public void SetBehaviorEnabled(bool enabled)
    {
        EnsureUsable();
        if (!enabled && behaviorEnabled) behavior?.Abort(AbortReason.NativeStateChanged);
        behaviorEnabled = enabled && behavior != null;
        globallySuspended = false;
    }

    public bool SetTarget(GameCharacter target)
    {
        EnsureUsable();
        if (target?.IsValid != true) return false;
        AIActor? attachedTarget = AIActorRegistry.Find(target);
        if (attachedTarget != null && !ReferenceEquals(Map, attachedTarget.Map)) return false;
        if (attachedTarget == null && !ReferenceEquals(Map, Polaris.PolarisAPI.Game.World.CurrentMap)) return false;
        this.target = target;
        return true;
    }

    public void ClearTarget() { EnsureUsable(); target = null; }

    public bool TryGetBehaviorAttribute<T>(string key, out T value)
    {
        if (IsValid && !string.IsNullOrEmpty(key) && behavior != null &&
            behaviorAttributes.TryGetValue(key, out object? raw) && raw is T typed)
        {
            value = typed;
            return true;
        }
        value = default!;
        return false;
    }

    public bool SetBehaviorAttribute<T>(string key, T value)
    {
        EnsureUsable();
        if (behavior == null || string.IsNullOrWhiteSpace(key) || !BehaviorValues.IsSupported(value)) return false;
        if (!behavior.TrySetAttribute(key, value)) return false;
        behaviorAttributes[key] = value;
        return true;
    }

    public bool RemoveBehaviorAttribute(string key)
    {
        EnsureUsable();
        if (behavior == null || string.IsNullOrEmpty(key) || !behavior.TryRemoveAttribute(key)) return false;
        return behaviorAttributes.Remove(key);
    }

    internal void Tick(float deltaTime)
    {
        if (!IsValid) { Release(AbortReason.CharacterDestroyed); return; }
        if (target?.IsValid != true) target = null;
        if (!behaviorEnabled || behavior == null) return;
        if (!AISettings.Enabled)
        {
            if (!globallySuspended) behavior.Abort(AbortReason.NativeStateChanged);
            globallySuspended = true;
            return;
        }
        globallySuspended = false;
        try { behavior.Tick(deltaTime); }
        catch (Exception ex)
        {
            behavior.Abort(AbortReason.NativeStateChanged);
            behaviorEnabled = false;
            Polaris.PolarisAPI.Errors.Report(ex, $"PolarisAI behavior '{behavior.BehaviorId}'");
        }
    }

    internal bool ControlsNativeAI => AISettings.Enabled && IsBehaviorEnabled;

    internal void ReloadBehavior(string behaviorId)
    {
        if (!IsValid || behavior == null || behavior.BehaviorId != behaviorId) return;
        var attributes = new Dictionary<string, object?>(behaviorAttributes, StringComparer.Ordinal);
        if (!BehaviorRepository.TryCreate(behaviorId, this, attributes, out BehaviorRuntime? next)) return;
        bool wasEnabled = behaviorEnabled;
        behavior.Abort(AbortReason.ConfigReload);
        behavior = next!;
        behaviorEnabled = wasEnabled;
        globallySuspended = false;
    }

    internal void Release(AbortReason reason)
    {
        if (detached) return;
        behavior?.Abort(reason);
        behavior = null;
        behaviorAttributes.Clear();
        target = null;
        detached = true;
        character = null;
        map = null;
    }

    protected void EnsureUsable()
    {
        if (!IsValid) throw new InvalidAIInstanceException(GetType().Name);
    }
}

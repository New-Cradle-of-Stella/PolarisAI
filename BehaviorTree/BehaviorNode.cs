using System;
using System.Collections.Generic;
using Polaris.API;

namespace Polaris.AI;

public enum NodeStatus
{
    Success,
    Failure,
    Running,
    Suspended,
}

public enum AbortReason
{
    Replaced,
    Detached,
    Despawned,
    CharacterDestroyed,
    ConfigReload,
    NativeStateChanged,
    Shutdown,
}

internal sealed class BehaviorContext
{
    internal BehaviorContext(AIActor actor, Dictionary<string, object?> attributes, int seed)
    {
        Actor = actor;
        Attributes = attributes;
        Random = new Random(seed);
    }

    internal AIActor Actor { get; }
    internal Dictionary<string, object?> Attributes { get; }
    internal Random Random { get; }
    internal float DeltaTime { get; set; }
    internal double Time { get; set; }
}

internal abstract class BehaviorNode
{
    protected BehaviorNode(string id) { Id = id; }
    internal string Id { get; }
    internal abstract NodeStatus Tick(BehaviorContext context);
    internal virtual void Abort(BehaviorContext context, AbortReason reason) { }
}

internal abstract class CompositeNode : BehaviorNode
{
    protected CompositeNode(string id, IReadOnlyList<BehaviorNode> children) : base(id) { Children = children; }
    protected IReadOnlyList<BehaviorNode> Children { get; }
    internal override void Abort(BehaviorContext context, AbortReason reason)
    {
        foreach (BehaviorNode child in Children) child.Abort(context, reason);
    }
}

internal sealed class SequenceNode : CompositeNode
{
    int index;
    internal SequenceNode(string id, IReadOnlyList<BehaviorNode> children) : base(id, children) { }
    internal override NodeStatus Tick(BehaviorContext context)
    {
        while (index < Children.Count)
        {
            NodeStatus status = Children[index].Tick(context);
            if (status == NodeStatus.Success) { index++; continue; }
            if (status == NodeStatus.Failure) index = 0;
            return status;
        }
        index = 0;
        return NodeStatus.Success;
    }
    internal override void Abort(BehaviorContext context, AbortReason reason) { base.Abort(context, reason); index = 0; }
}

internal sealed class FallbackNode : CompositeNode
{
    int index;
    internal FallbackNode(string id, IReadOnlyList<BehaviorNode> children) : base(id, children) { }
    internal override NodeStatus Tick(BehaviorContext context)
    {
        while (index < Children.Count)
        {
            NodeStatus status = Children[index].Tick(context);
            if (status == NodeStatus.Failure) { index++; continue; }
            if (status == NodeStatus.Success) index = 0;
            return status;
        }
        index = 0;
        return NodeStatus.Failure;
    }
    internal override void Abort(BehaviorContext context, AbortReason reason) { base.Abort(context, reason); index = 0; }
}

internal sealed class ReactiveNode : CompositeNode
{
    readonly bool sequence;
    internal ReactiveNode(string id, IReadOnlyList<BehaviorNode> children, bool sequence) : base(id, children) { this.sequence = sequence; }
    internal override NodeStatus Tick(BehaviorContext context)
    {
        foreach (BehaviorNode child in Children)
        {
            NodeStatus status = child.Tick(context);
            if (sequence && status != NodeStatus.Success) return status;
            if (!sequence && status != NodeStatus.Failure) return status;
        }
        return sequence ? NodeStatus.Success : NodeStatus.Failure;
    }
}

internal sealed class ParallelNode : CompositeNode
{
    internal ParallelNode(string id, IReadOnlyList<BehaviorNode> children) : base(id, children) { }
    internal override NodeStatus Tick(BehaviorContext context)
    {
        bool running = false;
        foreach (BehaviorNode child in Children)
        {
            NodeStatus status = child.Tick(context);
            if (status == NodeStatus.Failure) return NodeStatus.Failure;
            running |= status == NodeStatus.Running || status == NodeStatus.Suspended;
        }
        return running ? NodeStatus.Running : NodeStatus.Success;
    }
}

internal sealed class InverterNode : BehaviorNode
{
    readonly BehaviorNode child;
    internal InverterNode(string id, BehaviorNode child) : base(id) { this.child = child; }
    internal override NodeStatus Tick(BehaviorContext context) => child.Tick(context) switch
    {
        NodeStatus.Success => NodeStatus.Failure,
        NodeStatus.Failure => NodeStatus.Success,
        NodeStatus status => status,
    };
    internal override void Abort(BehaviorContext context, AbortReason reason) => child.Abort(context, reason);
}

internal sealed class SucceederNode : BehaviorNode
{
    readonly BehaviorNode child;
    internal SucceederNode(string id, BehaviorNode child) : base(id) { this.child = child; }
    internal override NodeStatus Tick(BehaviorContext context)
    {
        NodeStatus status = child.Tick(context);
        return status == NodeStatus.Failure ? NodeStatus.Success : status;
    }
    internal override void Abort(BehaviorContext context, AbortReason reason) => child.Abort(context, reason);
}

internal sealed class TimedDecoratorNode : BehaviorNode
{
    readonly BehaviorNode child;
    readonly Func<BehaviorContext, double> seconds;
    readonly bool cooldown;
    double started = -1;
    double readyAt;
    internal TimedDecoratorNode(string id, BehaviorNode child, Func<BehaviorContext, double> seconds, bool cooldown) : base(id)
    { this.child = child; this.seconds = seconds; this.cooldown = cooldown; }
    internal override NodeStatus Tick(BehaviorContext context)
    {
        if (cooldown && context.Time < readyAt) return NodeStatus.Failure;
        if (started < 0) started = context.Time;
        NodeStatus status = child.Tick(context);
        if (!cooldown && status == NodeStatus.Running && context.Time - started >= Math.Max(0, seconds(context)))
        {
            child.Abort(context, AbortReason.NativeStateChanged);
            started = -1;
            return NodeStatus.Failure;
        }
        if (status == NodeStatus.Success || status == NodeStatus.Failure)
        {
            started = -1;
            if (cooldown) readyAt = context.Time + Math.Max(0, seconds(context));
        }
        return status;
    }
    internal override void Abort(BehaviorContext context, AbortReason reason) { child.Abort(context, reason); started = -1; }
}

internal sealed class RepeatNode : BehaviorNode
{
    readonly BehaviorNode child;
    readonly Func<BehaviorContext, int> count;
    int completed;
    internal RepeatNode(string id, BehaviorNode child, Func<BehaviorContext, int> count) : base(id) { this.child = child; this.count = count; }
    internal override NodeStatus Tick(BehaviorContext context)
    {
        int limit = count(context);
        if (limit == 0) return NodeStatus.Success;
        NodeStatus status = child.Tick(context);
        if (status == NodeStatus.Failure) { completed = 0; return status; }
        if (status == NodeStatus.Success && (limit < 0 || ++completed < limit)) return NodeStatus.Running;
        if (status == NodeStatus.Success) completed = 0;
        return status;
    }
    internal override void Abort(BehaviorContext context, AbortReason reason) { child.Abort(context, reason); completed = 0; }
}

internal sealed class ChanceNode : BehaviorNode
{
    readonly BehaviorNode child;
    readonly Func<BehaviorContext, double> probability;
    bool? accepted;
    internal ChanceNode(string id, BehaviorNode child, Func<BehaviorContext, double> probability) : base(id)
    { this.child = child; this.probability = probability; }
    internal override NodeStatus Tick(BehaviorContext context)
    {
        accepted ??= context.Random.NextDouble() <= Math.Max(0, Math.Min(1, probability(context)));
        if (!accepted.Value) { accepted = null; return NodeStatus.Failure; }
        NodeStatus status = child.Tick(context);
        if (status == NodeStatus.Success || status == NodeStatus.Failure) accepted = null;
        return status;
    }
    internal override void Abort(BehaviorContext context, AbortReason reason) { child.Abort(context, reason); accepted = null; }
}

internal sealed class WeightedSelectorNode : CompositeNode
{
    int selected = -1;
    internal WeightedSelectorNode(string id, IReadOnlyList<BehaviorNode> children) : base(id, children) { }
    internal override NodeStatus Tick(BehaviorContext context)
    {
        if (Children.Count == 0) return NodeStatus.Failure;
        if (selected < 0) selected = context.Random.Next(Children.Count);
        NodeStatus status = Children[selected].Tick(context);
        if (status == NodeStatus.Success || status == NodeStatus.Failure) selected = -1;
        return status;
    }
    internal override void Abort(BehaviorContext context, AbortReason reason) { base.Abort(context, reason); selected = -1; }
}

internal sealed class DelegateNode : BehaviorNode
{
    readonly Func<BehaviorContext, NodeStatus> tick;
    internal DelegateNode(string id, Func<BehaviorContext, NodeStatus> tick) : base(id) { this.tick = tick; }
    internal override NodeStatus Tick(BehaviorContext context) => tick(context);
}

internal sealed class WaitNode : BehaviorNode
{
    readonly Func<BehaviorContext, double> seconds;
    double started = -1;
    internal WaitNode(string id, Func<BehaviorContext, double> seconds) : base(id) { this.seconds = seconds; }
    internal override NodeStatus Tick(BehaviorContext context)
    {
        if (started < 0) started = context.Time;
        if (context.Time - started < Math.Max(0, seconds(context))) return NodeStatus.Running;
        started = -1;
        return NodeStatus.Success;
    }
    internal override void Abort(BehaviorContext context, AbortReason reason) { started = -1; }
}

internal sealed class MoveToTargetNode : BehaviorNode
{
    readonly Func<BehaviorContext, double> speed;
    readonly Func<BehaviorContext, double> stopDistance;
    internal MoveToTargetNode(string id, Func<BehaviorContext, double> speed, Func<BehaviorContext, double> stopDistance) : base(id)
    { this.speed = speed; this.stopDistance = stopDistance; }
    internal override NodeStatus Tick(BehaviorContext context)
    {
        GameCharacter? target = context.Actor.Target;
        if (target == null) return NodeStatus.Failure;
        double dx = target.X - context.Actor.X;
        double dy = target.Y - context.Actor.Y;
        if (Math.Sqrt(dx * dx + dy * dy) <= Math.Max(0, stopDistance(context)))
        {
            context.Actor.SetVelocity(new GameVector2(0, context.Actor.VelocityY));
            return NodeStatus.Success;
        }
        context.Actor.SetFacing(dx >= 0 ? GameFacing.Right : GameFacing.Left);
        float velocity = (float)Math.Max(0, speed(context)) * (dx >= 0 ? 1f : -1f);
        context.Actor.SetVelocity(new GameVector2(velocity, context.Actor.VelocityY));
        return NodeStatus.Running;
    }
    internal override void Abort(BehaviorContext context, AbortReason reason)
    {
        if (context.Actor.IsValid)
            context.Actor.SetVelocity(new GameVector2(0, context.Actor.VelocityY));
    }
}

internal sealed class AttackTargetNode : BehaviorNode
{
    readonly Func<BehaviorContext, double> range;
    readonly Func<BehaviorContext, int> damage;
    readonly Func<BehaviorContext, double> cooldown;
    double readyAt;
    internal AttackTargetNode(string id, Func<BehaviorContext, double> range, Func<BehaviorContext, int> damage,
        Func<BehaviorContext, double> cooldown) : base(id)
    { this.range = range; this.damage = damage; this.cooldown = cooldown; }
    internal override NodeStatus Tick(BehaviorContext context)
    {
        GameCharacter? target = context.Actor.Target;
        if (target == null || !target.IsAlive) return NodeStatus.Failure;
        double dx = target.X - context.Actor.X;
        double dy = target.Y - context.Actor.Y;
        if (dx * dx + dy * dy > Math.Pow(Math.Max(0, range(context)), 2)) return NodeStatus.Failure;
        if (context.Time < readyAt) return NodeStatus.Running;
        int amount = Math.Max(0, damage(context));
        if (amount == 0) return NodeStatus.Failure;
        target.DamageHp(amount);
        readyAt = context.Time + Math.Max(0, cooldown(context));
        return target.IsAlive ? NodeStatus.Running : NodeStatus.Success;
    }
}

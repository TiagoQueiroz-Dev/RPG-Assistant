using System.Text.Json.Serialization;
using RpgWorld.Domain.Worlds;

namespace RpgWorld.Domain.Actors.Actions;

public enum NpcActionStatus { Running, Completed, Failed, Cancelled }
public enum NpcActionReplacementPolicy { ReplaceDifferent, KeepRunning, Restart }
public enum NpcActionTargetKind { Actor, Structure, WorldEntity }

public sealed record NpcActionTarget
{
    [JsonConstructor]
    public NpcActionTarget(Position? position = null, NpcActionTargetKind? entityKind = null, Guid? entityId = null)
    {
        if (position is null && entityId is null) throw new ArgumentException("A target needs a position or entity.");
        if (position is { WorldId: var worldId } && worldId == Guid.Empty)
            throw new ArgumentException("Target position needs a world.", nameof(position));
        if (entityKind.HasValue != entityId.HasValue || entityId == Guid.Empty ||
            (entityKind is { } kind && !Enum.IsDefined(kind)))
            throw new ArgumentException("An entity target needs a valid kind and identifier.");
        Position = position;
        EntityKind = entityKind;
        EntityId = entityId;
    }

    public Position? Position { get; }
    public NpcActionTargetKind? EntityKind { get; }
    public Guid? EntityId { get; }
}

public sealed record NpcActionExecution
{
    [JsonConstructor]
    public NpcActionExecution(Guid id, string actionCode, DateTimeOffset startedAt, DateTimeOffset updatedAt,
        NpcActionStatus status, decimal progress, NpcActionTarget? target = null,
        DateTimeOffset? lastProcessedAt = null, string? reason = null)
    {
        if (id == Guid.Empty) throw new ArgumentException("Execution identifier is required.", nameof(id));
        if (string.IsNullOrWhiteSpace(actionCode) || actionCode.Trim().Length > 120)
            throw new ArgumentException("Action code must contain 1 to 120 characters.", nameof(actionCode));
        if (!Enum.IsDefined(status)) throw new ArgumentOutOfRangeException(nameof(status));
        if (progress is < 0m or > 1m) throw new ArgumentOutOfRangeException(nameof(progress));
        if (updatedAt < startedAt || lastProcessedAt < startedAt || lastProcessedAt > updatedAt)
            throw new ArgumentOutOfRangeException(nameof(updatedAt));
        if (reason is { Length: > 500 }) throw new ArgumentException("Reason is too long.", nameof(reason));
        Id = id; ActionCode = actionCode.Trim(); StartedAt = startedAt.ToUniversalTime();
        UpdatedAt = updatedAt.ToUniversalTime(); Status = status; Progress = progress;
        Target = target; LastProcessedAt = lastProcessedAt?.ToUniversalTime(); Reason = reason;
    }

    public Guid Id { get; private init; }
    public string ActionCode { get; private init; }
    public DateTimeOffset StartedAt { get; private init; }
    public DateTimeOffset UpdatedAt { get; private init; }
    public NpcActionStatus Status { get; private init; }
    public decimal Progress { get; private init; }
    public NpcActionTarget? Target { get; private init; }
    public DateTimeOffset? LastProcessedAt { get; private init; }
    public string? Reason { get; private init; }

    public static NpcActionExecution Start(string code, DateTimeOffset instant, NpcActionTarget? target = null) =>
        new(Guid.CreateVersion7(), code, instant, instant, NpcActionStatus.Running, 0m, target);

    public bool CanProcess(DateTimeOffset instant) => Status == NpcActionStatus.Running &&
        instant >= UpdatedAt && (LastProcessedAt is null || instant > LastProcessedAt);

    internal NpcActionExecution Advance(decimal progress, DateTimeOffset instant)
    {
        RequireRunning(instant);
        if (progress < Progress || progress > 1m) throw new ArgumentOutOfRangeException(nameof(progress));
        if (!CanProcess(instant)) return this;
        return this with { Progress = progress, UpdatedAt = instant.ToUniversalTime(), LastProcessedAt = instant.ToUniversalTime() };
    }

    internal NpcActionExecution Retarget(NpcActionTarget target, DateTimeOffset instant)
    {
        RequireRunning(instant);
        return this with { Target = target, UpdatedAt = instant.ToUniversalTime() };
    }

    internal NpcActionExecution Finish(NpcActionStatus status, DateTimeOffset instant, string? reason)
    {
        RequireRunning(instant);
        if (!Enum.IsDefined(status) || status == NpcActionStatus.Running) throw new ArgumentOutOfRangeException(nameof(status));
        if (status != NpcActionStatus.Completed && string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("Failure and cancellation require a reason.", nameof(reason));
        if (reason is { Length: > 500 }) throw new ArgumentException("Reason is too long.", nameof(reason));
        return this with { Status = status, Progress = status == NpcActionStatus.Completed ? 1m : Progress,
            UpdatedAt = instant.ToUniversalTime(), Reason = reason };
    }

    private void RequireRunning(DateTimeOffset instant)
    {
        if (Status != NpcActionStatus.Running) throw new InvalidOperationException("Action is already terminal.");
        if (instant < UpdatedAt) throw new ArgumentOutOfRangeException(nameof(instant), "Action time cannot move backwards.");
    }
}

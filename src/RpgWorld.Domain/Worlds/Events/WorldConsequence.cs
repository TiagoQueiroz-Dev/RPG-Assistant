using RpgWorld.Domain.Events;

namespace RpgWorld.Domain.Worlds.Events;

public enum WorldConsequenceKind { Reputation, Crime, Family, Faction, Economy }

public sealed class WorldConsequence : AggregateRoot
{
    private WorldConsequence() { }

    private WorldConsequence(
        Guid worldId,
        WorldConsequenceKind kind,
        Guid targetId,
        decimal magnitude,
        string description,
        Guid sourceEventId,
        DateTimeOffset occurredAtUtc)
    {
        if (worldId == Guid.Empty) throw new ArgumentException("World identifier is required.", nameof(worldId));
        if (!Enum.IsDefined(kind)) throw new ArgumentOutOfRangeException(nameof(kind));
        if (targetId == Guid.Empty) throw new ArgumentException("Consequence target is required.", nameof(targetId));
        if (magnitude is < -100m or > 100m) throw new ArgumentOutOfRangeException(nameof(magnitude));
        if (string.IsNullOrWhiteSpace(description) || description.Trim().Length > 500)
            throw new ArgumentException("Consequence description is required and cannot exceed 500 characters.", nameof(description));
        if (sourceEventId == Guid.Empty) throw new ArgumentException("Source event is required.", nameof(sourceEventId));
        Id = Guid.CreateVersion7();
        WorldId = worldId;
        Kind = kind;
        TargetId = targetId;
        Magnitude = magnitude;
        Description = description.Trim();
        SourceEventId = sourceEventId;
        OccurredAtUtc = occurredAtUtc.ToUniversalTime();
        RaiseDomainEvent(new WorldConsequenceAppliedEvent(
            Id, WorldId, Kind, TargetId, Magnitude, Description, SourceEventId, OccurredAtUtc));
    }

    public Guid Id { get; private set; }
    public Guid WorldId { get; private set; }
    public WorldConsequenceKind Kind { get; private set; }
    public Guid TargetId { get; private set; }
    public decimal Magnitude { get; private set; }
    public string Description { get; private set; } = string.Empty;
    public Guid SourceEventId { get; private set; }
    public DateTimeOffset OccurredAtUtc { get; private set; }

    public static WorldConsequence Create(
        Guid worldId,
        WorldConsequenceKind kind,
        Guid targetId,
        decimal magnitude,
        string description,
        Guid sourceEventId,
        DateTimeOffset occurredAtUtc) =>
        new(worldId, kind, targetId, magnitude, description, sourceEventId, occurredAtUtc);
}

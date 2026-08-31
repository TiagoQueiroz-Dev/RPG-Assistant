using RpgWorld.Domain.Worlds.Events;

namespace RpgWorld.Domain.Events;

public sealed record WorldConsequenceAppliedEvent(
    Guid ConsequenceId,
    Guid WorldId,
    WorldConsequenceKind Kind,
    Guid TargetId,
    decimal Magnitude,
    string Description,
    Guid SourceEventId,
    DateTimeOffset OccurredAtUtc)
    : DomainEvent(OccurredAtUtc);

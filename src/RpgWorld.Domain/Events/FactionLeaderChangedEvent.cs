namespace RpgWorld.Domain.Events;

public sealed record FactionLeaderChangedEvent(
    Guid FactionId,
    Guid WorldId,
    Guid PreviousLeaderActorId,
    Guid NewLeaderActorId,
    string Reason,
    DateTimeOffset OccurredAtUtc)
    : DomainEvent(OccurredAtUtc);

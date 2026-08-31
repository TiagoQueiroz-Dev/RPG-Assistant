namespace RpgWorld.Domain.Events;

public sealed record FactionCreatedEvent(
    Guid FactionId,
    Guid WorldId,
    string Name,
    string Type,
    Guid LeaderActorId,
    DateTimeOffset OccurredAtUtc)
    : DomainEvent(OccurredAtUtc);

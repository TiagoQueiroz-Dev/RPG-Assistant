namespace RpgWorld.Domain.Events;

public sealed record FactionMemberJoinedEvent(
    Guid FactionId,
    Guid WorldId,
    Guid ActorId,
    DateTimeOffset OccurredAtUtc)
    : DomainEvent(OccurredAtUtc);

namespace RpgWorld.Domain.Events;

public sealed record FactionMemberLeftEvent(
    Guid FactionId,
    Guid WorldId,
    Guid ActorId,
    string Reason,
    DateTimeOffset OccurredAtUtc)
    : DomainEvent(OccurredAtUtc);

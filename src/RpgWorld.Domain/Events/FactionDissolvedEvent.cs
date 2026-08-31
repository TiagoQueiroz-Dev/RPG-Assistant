namespace RpgWorld.Domain.Events;

public sealed record FactionDissolvedEvent(
    Guid FactionId,
    Guid WorldId,
    string Reason,
    DateTimeOffset OccurredAtUtc)
    : DomainEvent(OccurredAtUtc);

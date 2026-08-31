namespace RpgWorld.Domain.Events;

public sealed record CityCrisisEvent(
    Guid CityId,
    Guid WorldId,
    string Reason,
    int Severity,
    DateTimeOffset OccurredAtUtc)
    : DomainEvent(OccurredAtUtc);

namespace RpgWorld.Domain.Events;

public sealed record CityDestroyedEvent(
    Guid CityId,
    Guid WorldId,
    string Reason,
    int FinalPopulation,
    DateTimeOffset DestroyedAtUtc)
    : DomainEvent(DestroyedAtUtc);

namespace RpgWorld.Domain.Events;

public sealed record CityGrowthEvent(
    Guid CityId,
    Guid WorldId,
    int PreviousPopulation,
    int CurrentPopulation,
    string Reason,
    DateTimeOffset OccurredAtUtc)
    : DomainEvent(OccurredAtUtc);

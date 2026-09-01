namespace RpgWorld.Domain.Events;

public sealed record CityTradeRoutesChangedEvent(
    Guid CityId,
    Guid WorldId,
    int PreviousRouteCount,
    int CurrentRouteCount,
    DateTimeOffset OccurredAtUtc)
    : DomainEvent(OccurredAtUtc);

public sealed record CitySatisfactionChangedEvent(
    Guid CityId,
    Guid WorldId,
    decimal PreviousSatisfaction,
    decimal CurrentSatisfaction,
    string Reason,
    DateTimeOffset OccurredAtUtc)
    : DomainEvent(OccurredAtUtc);

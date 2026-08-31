namespace RpgWorld.Domain.Events;

public sealed record CityResourceShortageEvent(
    Guid CityId,
    Guid WorldId,
    string ResourceCode,
    decimal Demand,
    decimal Consumed,
    decimal ClosingStock,
    decimal UnitPrice,
    DateTimeOffset OccurredAtUtc)
    : DomainEvent(OccurredAtUtc);

namespace RpgWorld.Domain.Events;

public sealed record CityResourceSurplusEvent(
    Guid CityId,
    Guid WorldId,
    string ResourceCode,
    decimal Produced,
    decimal ClosingStock,
    decimal UnitPrice,
    DateTimeOffset OccurredAtUtc)
    : DomainEvent(OccurredAtUtc);

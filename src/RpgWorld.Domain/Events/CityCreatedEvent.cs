namespace RpgWorld.Domain.Events;

public sealed record CityCreatedEvent(
    Guid CityId,
    Guid WorldId,
    string Name,
    DateTimeOffset CreatedAtUtc)
    : DomainEvent(CreatedAtUtc);


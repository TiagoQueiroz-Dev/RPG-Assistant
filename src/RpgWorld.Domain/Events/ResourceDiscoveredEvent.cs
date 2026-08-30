namespace RpgWorld.Domain.Events;

public sealed record ResourceDiscoveredEvent(
    Guid ResourceId,
    Guid DiscoveredByActorId,
    Guid WorldId,
    DateTimeOffset DiscoveredAtUtc)
    : DomainEvent(DiscoveredAtUtc);


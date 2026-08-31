using RpgWorld.Domain.Worlds.Resources;

namespace RpgWorld.Domain.Events;

public sealed record ResourceSpawnedEvent(
    Guid ResourceId,
    Guid WorldId,
    string ResourceCode,
    ResourceDepositScope Scope,
    Guid? SourceWorldEventId,
    DateTimeOffset SpawnedAtUtc)
    : DomainEvent(SpawnedAtUtc);

namespace RpgWorld.Domain.Events;

public sealed record ChunkDeactivatedEvent(
    Guid WorldId,
    int ChunkX,
    int ChunkY,
    TimeSpan InactiveFor,
    DateTimeOffset DeactivatedAtUtc)
    : DomainEvent(DeactivatedAtUtc);

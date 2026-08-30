namespace RpgWorld.Domain.Events;

public sealed record ChunkActivatedEvent(
    Guid WorldId,
    int ChunkX,
    int ChunkY,
    int TileCount,
    DateTimeOffset ActivatedAtUtc)
    : DomainEvent(ActivatedAtUtc);

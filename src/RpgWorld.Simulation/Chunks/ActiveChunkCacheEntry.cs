namespace RpgWorld.Simulation.Chunks;

public sealed record ActiveChunkCacheEntry(
    Guid WorldId,
    int ChunkX,
    int ChunkY,
    int TileCount,
    DateTimeOffset ActivatedAtUtc,
    DateTimeOffset LastRelevantAtUtc);

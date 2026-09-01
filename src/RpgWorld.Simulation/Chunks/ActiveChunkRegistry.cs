using System.Collections.Concurrent;
using RpgWorld.Domain.Worlds;

namespace RpgWorld.Simulation.Chunks;

public sealed class ActiveChunkRegistry : IDisposable
{
    internal ConcurrentDictionary<ActiveChunkKey, ActiveChunk> Chunks { get; } = new();
    internal SemaphoreSlim Gate { get; } = new(1, 1);

    public int Count(Guid worldId) => Chunks.Count(pair => pair.Key.WorldId == worldId);

    public void Dispose() => Gate.Dispose();
}

internal readonly record struct ActiveChunkKey(Guid WorldId, ChunkCoordinate Coordinate);

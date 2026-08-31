using RpgWorld.Domain.Worlds;

namespace RpgWorld.Simulation.Chunks;

public interface IChunkActivationService
{
    IReadOnlyCollection<ActiveChunk> GetActiveChunks(Guid worldId);

    bool TryGetActiveChunk(
        Guid worldId,
        ChunkCoordinate coordinate,
        out ActiveChunk? activeChunk);

    Task ApplyActorMovementAsync(
        Guid worldId,
        Guid actorId,
        Position origin,
        Position destination,
        CancellationToken cancellationToken = default);

    Task SynchronizeAsync(
        World world,
        IEnumerable<Position> playerPositions,
        IEnumerable<ChunkCoordinate>? relevantRegions = null,
        CancellationToken cancellationToken = default);
}

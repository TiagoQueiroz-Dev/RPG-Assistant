using RpgWorld.Domain.Worlds;

namespace RpgWorld.Application.Worlds;

public interface IRegionSimulationRepository
{
    Task<IReadOnlyList<Chunk>> ListChunksAsync(Guid worldId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Tile>> ListTilesAsync(Guid worldId, ChunkCoordinate coordinate, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

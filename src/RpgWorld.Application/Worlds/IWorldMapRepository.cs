using RpgWorld.Domain.Worlds;

namespace RpgWorld.Application.Worlds;

public interface IWorldMapRepository
{
    Task<World?> GetWorldAsync(Guid worldId, CancellationToken cancellationToken = default);

    Task<Chunk?> GetChunkAsync(
        Guid worldId,
        ChunkCoordinate coordinate,
        CancellationToken cancellationToken = default);

    Task<Tile?> GetTileAsync(
        Position position,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Tile>> GetTilesAsync(
        Guid worldId,
        ChunkCoordinate coordinate,
        CancellationToken cancellationToken = default);
}

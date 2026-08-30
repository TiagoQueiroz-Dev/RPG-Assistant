using Microsoft.EntityFrameworkCore;
using RpgWorld.Application.Worlds;
using RpgWorld.Domain.Worlds;

namespace RpgWorld.Infrastructure.Persistence.Repositories;

public sealed class EfWorldMapRepository(RpgWorldDbContext dbContext)
    : IWorldMapRepository
{
    public Task<World?> GetWorldAsync(
        Guid worldId,
        CancellationToken cancellationToken = default) =>
        dbContext.Worlds
            .AsNoTracking()
            .SingleOrDefaultAsync(world => world.Id == worldId, cancellationToken);

    public Task<Chunk?> GetChunkAsync(
        Guid worldId,
        ChunkCoordinate coordinate,
        CancellationToken cancellationToken = default) =>
        dbContext.Chunks
            .AsNoTracking()
            .SingleOrDefaultAsync(
                chunk =>
                    chunk.WorldId == worldId &&
                    chunk.CoordinateX == coordinate.X &&
                    chunk.CoordinateY == coordinate.Y,
                cancellationToken);

    public Task<Tile?> GetTileAsync(
        Position position,
        CancellationToken cancellationToken = default) =>
        dbContext.Tiles
            .AsNoTracking()
            .SingleOrDefaultAsync(
                tile =>
                    tile.WorldId == position.WorldId &&
                    tile.X == position.X &&
                    tile.Y == position.Y,
                cancellationToken);

    public async Task<IReadOnlyList<Tile>> GetTilesAsync(
        Guid worldId,
        ChunkCoordinate coordinate,
        CancellationToken cancellationToken = default)
    {
        var chunk = await GetChunkAsync(worldId, coordinate, cancellationToken);
        if (chunk is null)
        {
            return [];
        }

        var maxX = chunk.OriginX + chunk.Width;
        var maxY = chunk.OriginY + chunk.Height;

        return await dbContext.Tiles
            .AsNoTracking()
            .Where(tile =>
                tile.WorldId == worldId &&
                tile.X >= chunk.OriginX &&
                tile.X < maxX &&
                tile.Y >= chunk.OriginY &&
                tile.Y < maxY)
            .OrderBy(tile => tile.Y)
            .ThenBy(tile => tile.X)
            .ToListAsync(cancellationToken);
    }
}


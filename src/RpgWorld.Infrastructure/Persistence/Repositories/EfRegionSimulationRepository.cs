using Microsoft.EntityFrameworkCore;
using RpgWorld.Application.Worlds;
using RpgWorld.Domain.Worlds;

namespace RpgWorld.Infrastructure.Persistence.Repositories;

public sealed class EfRegionSimulationRepository(RpgWorldDbContext dbContext)
    : IRegionSimulationRepository
{
    public async Task<IReadOnlyList<Chunk>> ListChunksAsync(Guid worldId, CancellationToken cancellationToken = default) =>
        await dbContext.Chunks
            .Where(chunk => chunk.WorldId == worldId)
            .OrderBy(chunk => chunk.CoordinateY)
            .ThenBy(chunk => chunk.CoordinateX)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Tile>> ListTilesAsync(
        Guid worldId,
        ChunkCoordinate coordinate,
        CancellationToken cancellationToken = default)
    {
        var chunk = await dbContext.Chunks.SingleAsync(
            candidate => candidate.WorldId == worldId &&
                candidate.CoordinateX == coordinate.X &&
                candidate.CoordinateY == coordinate.Y,
            cancellationToken);
        return await dbContext.Tiles
            .Where(tile => tile.WorldId == worldId &&
                tile.X >= chunk.OriginX && tile.X < chunk.OriginX + chunk.Width &&
                tile.Y >= chunk.OriginY && tile.Y < chunk.OriginY + chunk.Height)
            .OrderBy(tile => tile.Y)
            .ThenBy(tile => tile.X)
            .ToListAsync(cancellationToken);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await dbContext.SaveChangesAsync(cancellationToken);
        foreach (var entry in dbContext.ChangeTracker.Entries<Chunk>())
        {
            entry.State = EntityState.Detached;
        }
    }
}

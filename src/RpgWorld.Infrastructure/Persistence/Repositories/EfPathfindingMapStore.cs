using Microsoft.EntityFrameworkCore;
using RpgWorld.Application.Actors.Movement;
using RpgWorld.Domain.Worlds;

namespace RpgWorld.Infrastructure.Persistence.Repositories;

public sealed class EfPathfindingMapStore(RpgWorldDbContext dbContext) : IPathfindingMapStore
{
    public Task<World?> GetWorldAsync(Guid worldId, CancellationToken cancellationToken = default) =>
        dbContext.Worlds.AsNoTracking().SingleOrDefaultAsync(world => world.Id == worldId, cancellationToken);

    public async Task<IReadOnlyList<Tile>> GetTilesAsync(Guid worldId, NavigationBounds bounds, int limit,
        CancellationToken cancellationToken = default)
    {
        if (limit is < 1 or > 1_000_000) throw new ArgumentOutOfRangeException(nameof(limit));
        return await dbContext.Tiles.AsNoTracking().Where(tile => tile.WorldId == worldId &&
            tile.X >= bounds.MinX && tile.X <= bounds.MaxX && tile.Y >= bounds.MinY && tile.Y <= bounds.MaxY)
            .OrderBy(tile => tile.Y).ThenBy(tile => tile.X).Take(limit).ToArrayAsync(cancellationToken);
    }
}

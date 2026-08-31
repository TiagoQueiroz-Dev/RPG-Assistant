using Microsoft.EntityFrameworkCore;
using RpgWorld.Application.Actors.Housing;
using RpgWorld.Domain.Actors;
using RpgWorld.Domain.Actors.Housing;
using RpgWorld.Domain.Worlds;

namespace RpgWorld.Infrastructure.Persistence.Repositories;

public sealed class EfNpcHousingRepository(RpgWorldDbContext dbContext) : INpcHousingRepository
{
    public Task<World?> GetWorldAsync(Guid worldId, CancellationToken cancellationToken = default) =>
        dbContext.Worlds.SingleOrDefaultAsync(world => world.Id == worldId, cancellationToken);

    public async Task<IReadOnlyList<NpcActor>> ListHomelessAsync(Guid worldId, CancellationToken cancellationToken = default) =>
        await dbContext.Actors.OfType<NpcActor>()
            .Where(npc => npc.WorldId == worldId && npc.Status != ActorStatus.Dead && npc.HomeX == null)
            .OrderBy(npc => npc.Id).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<HousingConstruction>> ListInProgressAsync(Guid worldId, CancellationToken cancellationToken = default) =>
        await dbContext.HousingConstructions
            .Where(construction => construction.WorldId == worldId && construction.Status == HousingConstructionStatus.InProgress)
            .OrderBy(construction => construction.Id).ToListAsync(cancellationToken);

    public Task<NpcActor?> GetNpcAsync(Guid actorId, CancellationToken cancellationToken = default) =>
        dbContext.Actors.OfType<NpcActor>().SingleOrDefaultAsync(npc => npc.Id == actorId, cancellationToken);

    public Task<Tile?> FindBuildableTileAsync(Guid worldId, int originX, int originY, int radius, IReadOnlyCollection<string> allowedTerrains, CancellationToken cancellationToken = default) =>
        dbContext.Tiles.Where(tile =>
                tile.WorldId == worldId && tile.StructureId == null &&
                Math.Abs(tile.X - originX) <= radius && Math.Abs(tile.Y - originY) <= radius &&
                allowedTerrains.Contains(tile.TerrainCode))
            .OrderBy(tile => Math.Abs(tile.X - originX) + Math.Abs(tile.Y - originY))
            .ThenBy(tile => tile.Y).ThenBy(tile => tile.X)
            .FirstOrDefaultAsync(cancellationToken);

    public Task<Tile?> GetTileAsync(Position position, CancellationToken cancellationToken = default) =>
        dbContext.Tiles.SingleOrDefaultAsync(tile => tile.WorldId == position.WorldId && tile.X == position.X && tile.Y == position.Y, cancellationToken);

    public void Add(HousingConstruction construction) => dbContext.HousingConstructions.Add(construction);
    public async Task SaveChangesAsync(CancellationToken cancellationToken = default) => await dbContext.SaveChangesAsync(cancellationToken);
}

using Microsoft.EntityFrameworkCore;
using RpgWorld.Application.Worlds.Cities;
using RpgWorld.Domain.Actors;
using RpgWorld.Domain.Worlds;
using RpgWorld.Domain.Worlds.Cities;

namespace RpgWorld.Infrastructure.Persistence.Repositories;

public sealed class EfCityRepository(RpgWorldDbContext dbContext) : ICityRepository
{
    public Task<World?> GetWorldAsync(Guid worldId, CancellationToken cancellationToken = default) =>
        dbContext.Worlds.SingleOrDefaultAsync(world => world.Id == worldId, cancellationToken);

    public Task<City?> GetAsync(Guid cityId, CancellationToken cancellationToken = default) =>
        dbContext.Cities.Include("_territoryTiles")
            .SingleOrDefaultAsync(city => city.Id == cityId, cancellationToken);

    public async Task<IReadOnlyList<City>> ListByWorldAsync(
        Guid worldId,
        CancellationToken cancellationToken = default) =>
        await dbContext.Cities.AsNoTracking().Include("_territoryTiles")
            .Where(city => city.WorldId == worldId)
            .OrderBy(city => city.Name).ThenBy(city => city.Id)
            .ToListAsync(cancellationToken);

    public Task<NpcActor?> GetNpcAsync(Guid actorId, CancellationToken cancellationToken = default) =>
        dbContext.Actors.OfType<NpcActor>().SingleOrDefaultAsync(npc => npc.Id == actorId, cancellationToken);

    public async Task<IReadOnlyList<NpcActor>> ListResidentsAsync(
        Guid cityId,
        CancellationToken cancellationToken = default) =>
        await dbContext.Actors.OfType<NpcActor>()
            .Where(npc => npc.ResidentCityId == cityId)
            .OrderBy(npc => npc.Id)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Tile>> ListTilesAsync(
        Guid worldId,
        IReadOnlyCollection<Position> positions,
        CancellationToken cancellationToken = default)
    {
        if (positions.Count == 0) return [];
        var minX = positions.Min(position => position.X);
        var maxX = positions.Max(position => position.X);
        var minY = positions.Min(position => position.Y);
        var maxY = positions.Max(position => position.Y);
        var expected = positions.ToHashSet();
        return (await dbContext.Tiles.Where(tile =>
                tile.WorldId == worldId && tile.X >= minX && tile.X <= maxX && tile.Y >= minY && tile.Y <= maxY)
            .ToListAsync(cancellationToken))
            .Where(tile => expected.Contains(tile.Position))
            .ToArray();
    }

    public async Task<bool> TerritoryOverlapsAsync(
        Guid worldId,
        IReadOnlyCollection<Position> positions,
        CancellationToken cancellationToken = default)
    {
        if (positions.Count == 0) return false;
        var minX = positions.Min(position => position.X);
        var maxX = positions.Max(position => position.X);
        var minY = positions.Min(position => position.Y);
        var maxY = positions.Max(position => position.Y);
        var expected = positions.Select(position => (position.X, position.Y)).ToHashSet();
        var occupied = await dbContext.CityTerritoryTiles.AsNoTracking().Where(tile =>
                tile.WorldId == worldId && tile.IsActive &&
                tile.X >= minX && tile.X <= maxX && tile.Y >= minY && tile.Y <= maxY)
            .Select(tile => new { tile.X, tile.Y })
            .ToListAsync(cancellationToken);
        return occupied.Any(tile => expected.Contains((tile.X, tile.Y)));
    }

    public void Add(City city) => dbContext.Cities.Add(city);

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        await dbContext.SaveChangesAsync(cancellationToken);
}

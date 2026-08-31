using Microsoft.EntityFrameworkCore;
using RpgWorld.Application.Worlds.Factions;
using RpgWorld.Domain.Actors;
using RpgWorld.Domain.Worlds;
using RpgWorld.Domain.Worlds.Cities;
using RpgWorld.Domain.Worlds.Factions;

namespace RpgWorld.Infrastructure.Persistence.Repositories;

public sealed class EfFactionRepository(RpgWorldDbContext dbContext) : IFactionRepository, IFactionWarRepository
{
    public Task<World?> GetWorldAsync(Guid worldId, CancellationToken cancellationToken = default) =>
        dbContext.Worlds.SingleOrDefaultAsync(world => world.Id == worldId, cancellationToken);

    public Task<Faction?> GetAsync(Guid factionId, CancellationToken cancellationToken = default) =>
        dbContext.Factions.Include("_territoryTiles")
            .SingleOrDefaultAsync(faction => faction.Id == factionId, cancellationToken);

    public async Task<IReadOnlyList<Faction>> ListByWorldAsync(
        Guid worldId,
        CancellationToken cancellationToken = default) =>
        await dbContext.Factions.AsNoTracking().Include("_territoryTiles")
            .Where(faction => faction.WorldId == worldId)
            .OrderBy(faction => faction.Name).ThenBy(faction => faction.Id)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Faction>> ListActiveAsync(
        Guid worldId,
        CancellationToken cancellationToken = default) =>
        await dbContext.Factions.Include("_territoryTiles")
            .Where(faction => faction.WorldId == worldId && faction.Status == FactionStatus.Active)
            .OrderBy(faction => faction.Id).ToListAsync(cancellationToken);

    public async Task<FactionWarContext> BuildContextAsync(
        Faction source,
        Faction target,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(target);
        var targetTerritory = target.Territory.Select(position => (position.X, position.Y)).ToHashSet();
        var sharedEdges = source.Territory.Sum(position =>
            Adjacent(position).Count(targetTerritory.Contains));
        var cities = await dbContext.Cities.AsNoTracking()
            .Where(city => city.Status != CityStatus.Destroyed &&
                (city.GoverningFactionId == source.Id || city.GoverningFactionId == target.Id))
            .ToListAsync(cancellationToken);
        var sourceShortages = cities.Where(city => city.GoverningFactionId == source.Id)
            .Sum(city => city.ResourceMarkets.Values.Count(market =>
                market.Condition == CityMarketCondition.Shortage));
        var targetResources = cities.Where(city => city.GoverningFactionId == target.Id)
            .Sum(city => city.ResourceStocks.Values.Sum());
        var leader = source.LeaderActorId is { } leaderId
            ? await dbContext.Actors.AsNoTracking().OfType<NpcActor>()
                .SingleOrDefaultAsync(actor => actor.Id == leaderId, cancellationToken)
            : null;
        var aggressive = leader?.TraitCodes.Any(code => code is "aggressive" or "warlike" or "ruthless") ?? false;
        return new FactionWarContext(sharedEdges, sourceShortages, targetResources, aggressive);
    }

    private static IEnumerable<(int X, int Y)> Adjacent(Position position)
    {
        yield return (position.X + 1, position.Y);
        yield return (position.X - 1, position.Y);
        yield return (position.X, position.Y + 1);
        yield return (position.X, position.Y - 1);
    }

    public Task<bool> NameExistsAsync(
        Guid worldId,
        string name,
        CancellationToken cancellationToken = default) =>
        dbContext.Factions.AnyAsync(faction => faction.WorldId == worldId && faction.Name == name, cancellationToken);

    public Task<Actor?> GetActorAsync(Guid actorId, CancellationToken cancellationToken = default) =>
        dbContext.Actors.SingleOrDefaultAsync(actor => actor.Id == actorId, cancellationToken);

    public async Task<IReadOnlyList<Actor>> ListMembersAsync(
        Guid factionId,
        CancellationToken cancellationToken = default) =>
        await dbContext.Actors.Where(actor => actor.FactionId == factionId)
            .OrderBy(actor => actor.Id).ToListAsync(cancellationToken);

    public Task<City?> GetCityAsync(Guid cityId, CancellationToken cancellationToken = default) =>
        dbContext.Cities.Include("_territoryTiles")
            .SingleOrDefaultAsync(city => city.Id == cityId, cancellationToken);

    public async Task<IReadOnlyList<City>> ListCitiesAsync(
        Guid factionId,
        CancellationToken cancellationToken = default) =>
        await dbContext.Cities.Include("_territoryTiles")
            .Where(city => city.GoverningFactionId == factionId)
            .OrderBy(city => city.Id).ToListAsync(cancellationToken);

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
        return (await dbContext.Tiles.Where(tile => tile.WorldId == worldId &&
                tile.X >= minX && tile.X <= maxX && tile.Y >= minY && tile.Y <= maxY)
            .ToListAsync(cancellationToken))
            .Where(tile => expected.Contains(tile.Position)).ToArray();
    }

    public async Task<bool> TerritoryOverlapsAsync(
        Guid worldId,
        IReadOnlyCollection<Position> positions,
        Guid? excludingFactionId = null,
        CancellationToken cancellationToken = default)
    {
        if (positions.Count == 0) return false;
        var minX = positions.Min(position => position.X);
        var maxX = positions.Max(position => position.X);
        var minY = positions.Min(position => position.Y);
        var maxY = positions.Max(position => position.Y);
        var expected = positions.Select(position => (position.X, position.Y)).ToHashSet();
        var query = dbContext.FactionTerritoryTiles.AsNoTracking().Where(tile =>
            tile.WorldId == worldId && tile.IsActive &&
            tile.X >= minX && tile.X <= maxX && tile.Y >= minY && tile.Y <= maxY);
        if (excludingFactionId is { } excluded)
            query = query.Where(tile => tile.FactionId != excluded);
        var occupied = await query.Select(tile => new { tile.X, tile.Y }).ToListAsync(cancellationToken);
        return occupied.Any(tile => expected.Contains((tile.X, tile.Y)));
    }

    public void Add(Faction faction) => dbContext.Factions.Add(faction);

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        await dbContext.SaveChangesAsync(cancellationToken);
}

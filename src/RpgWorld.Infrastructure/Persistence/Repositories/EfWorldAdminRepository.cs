using System.Globalization;
using Microsoft.EntityFrameworkCore;
using RpgWorld.Application.Worlds.Admin;
using RpgWorld.Domain.Actors;
using RpgWorld.Domain.Worlds;
using RpgWorld.Domain.Worlds.Cities;
using RpgWorld.Domain.Worlds.Factions;

namespace RpgWorld.Infrastructure.Persistence.Repositories;

public sealed class EfWorldAdminRepository(RpgWorldDbContext dbContext) : IWorldAdminRepository
{
    public async Task<WorldAdminView?> InspectAsync(
        WorldAdminQuery query,
        CancellationToken cancellationToken = default)
    {
        var world = await dbContext.Worlds.AsNoTracking().SingleOrDefaultAsync(
            value => value.Id == query.WorldId, cancellationToken);
        if (world is null) return null;
        var clock = await dbContext.WorldClocks.AsNoTracking().SingleOrDefaultAsync(
            value => value.WorldId == world.Id, cancellationToken);
        var summary = await BuildSummaryAsync(world.Id, cancellationToken);
        var (entities, total) = await ListEntitiesAsync(world, query, cancellationToken);
        return new WorldAdminView(
            world.Id,
            world.Name,
            world.IsSimulationRunning,
            clock?.CurrentInstant,
            new WorldAdminMapView(world.Width, world.Height, world.ChunkSize,
                (long)world.Width * world.Height, world.ChunkColumns * world.ChunkRows),
            summary,
            query.EntityType,
            entities,
            query.Page,
            query.PageSize,
            total,
            total == 0 ? 0 : checked((int)Math.Ceiling(total / (decimal)query.PageSize)),
            WorldAdminService.EntityTypes);
    }

    private async Task<WorldAdminSummary> BuildSummaryAsync(Guid worldId, CancellationToken cancellationToken)
    {
        var totalActors = await dbContext.Actors.CountAsync(actor => actor.WorldId == worldId, cancellationToken);
        var npcs = await dbContext.Actors.OfType<NpcActor>().CountAsync(actor => actor.WorldId == worldId, cancellationToken);
        var players = await dbContext.Actors.OfType<PlayerActor>().CountAsync(actor => actor.WorldId == worldId, cancellationToken);
        var creatures = await dbContext.Actors.OfType<CreatureActor>().CountAsync(actor => actor.WorldId == worldId, cancellationToken);
        var activeChunks = await dbContext.Chunks.CountAsync(chunk =>
            chunk.WorldId == worldId && chunk.SimulationLevel != SimulationLevel.Abstract, cancellationToken);
        var resourceCount = await dbContext.ResourceDeposits.CountAsync(value => value.WorldId == worldId, cancellationToken);
        var resourceQuantity = await dbContext.ResourceDeposits.Where(value => value.WorldId == worldId)
            .SumAsync(value => (decimal?)value.Quantity, cancellationToken) ?? 0m;
        var cities = dbContext.Cities.AsNoTracking().Where(city => city.WorldId == worldId);
        var cityCount = await cities.CountAsync(cancellationToken);
        var population = await cities.SumAsync(city => (int?)city.Population, cancellationToken) ?? 0;
        var cityWealth = await cities.SumAsync(city => (decimal?)city.Wealth, cancellationToken) ?? 0m;
        var factions = await dbContext.Factions.AsNoTracking().Where(faction => faction.WorldId == worldId)
            .ToListAsync(cancellationToken);
        return new WorldAdminSummary(
            totalActors, npcs, players, creatures, activeChunks, resourceCount, resourceQuantity,
            cityCount, population, cityWealth, factions.Count,
            factions.Count(faction => faction.Type == FactionType.Army),
            factions.Sum(faction => faction.MilitaryPower),
            factions.Sum(faction => faction.Relations.Count),
            factions.Sum(faction => faction.Relations.Values.Count(relation => relation.Kind == FactionRelationKind.War)));
    }

    private Task<(IReadOnlyList<WorldAdminEntityView> Items, long Total)> ListEntitiesAsync(
        World world,
        WorldAdminQuery query,
        CancellationToken cancellationToken) => query.EntityType switch
        {
            "chunks" => ListChunksAsync(world, query, cancellationToken),
            "npcs" => ListActorsAsync<NpcActor>(world, query, cancellationToken),
            "players" => ListActorsAsync<PlayerActor>(world, query, cancellationToken),
            "creatures" => ListActorsAsync<CreatureActor>(world, query, cancellationToken),
            "resources" => ListResourcesAsync(world, query, cancellationToken),
            "cities" => ListCitiesAsync(world, query, cancellationToken),
            "factions" => ListFactionsAsync(world, query, false, cancellationToken),
            "armies" => ListFactionsAsync(world, query, true, cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(query))
        };

    private async Task<(IReadOnlyList<WorldAdminEntityView>, long)> ListChunksAsync(
        World world, WorldAdminQuery filter, CancellationToken token)
    {
        var query = dbContext.Chunks.AsNoTracking().Where(value => value.WorldId == world.Id);
        if (filter.RegionX is { } x && filter.RegionY is { } y)
            query = query.Where(value => value.CoordinateX == x && value.CoordinateY == y);
        var total = await query.LongCountAsync(token);
        var page = await query.OrderBy(value => value.CoordinateY).ThenBy(value => value.CoordinateX)
            .Skip(Skip(filter)).Take(filter.PageSize).ToListAsync(token);
        return (page.Select(value => Entity(value.Id, "chunk", $"Region {value.CoordinateX},{value.CoordinateY}",
            value.SimulationLevel.ToString(), value.OriginX, value.OriginY, value.CoordinateX, value.CoordinateY, null,
            $"/api/worlds/{world.Id}/map", new Dictionary<string, string>
            {
                ["population"] = value.AggregatePopulation.ToString(CultureInfo.InvariantCulture),
                ["economy"] = value.AggregateEconomicOutput.ToString(CultureInfo.InvariantCulture),
                ["military"] = value.AggregateMilitaryStrength.ToString(CultureInfo.InvariantCulture)
            })).ToArray(), total);
    }

    private async Task<(IReadOnlyList<WorldAdminEntityView>, long)> ListActorsAsync<TActor>(
        World world, WorldAdminQuery filter, CancellationToken token) where TActor : Actor
    {
        var query = dbContext.Actors.AsNoTracking().OfType<TActor>().Where(value => value.WorldId == world.Id);
        if (filter.RegionX is { } x && filter.RegionY is { } y)
            query = query.Where(value => value.X >= x * world.ChunkSize && value.X < (x + 1) * world.ChunkSize &&
                value.Y >= y * world.ChunkSize && value.Y < (y + 1) * world.ChunkSize);
        if (filter.FactionId is { } factionId) query = query.Where(value => value.FactionId == factionId);
        var total = await query.LongCountAsync(token);
        var page = await query.OrderBy(value => value.Name).ThenBy(value => value.Id)
            .Skip(Skip(filter)).Take(filter.PageSize).ToListAsync(token);
        return (page.Select(value => Entity(value.Id, value.Kind, value.Name, value.Status.ToString(),
            value.X, value.Y, value.X / world.ChunkSize, value.Y / world.ChunkSize, value.FactionId,
            $"/api/actors/{value.Id}/inspector", new Dictionary<string, string>
            {
                ["health"] = $"{value.Health}/{value.MaximumHealth}",
                ["action"] = value.CurrentAction ?? string.Empty
            })).ToArray(), total);
    }

    private async Task<(IReadOnlyList<WorldAdminEntityView>, long)> ListResourcesAsync(
        World world, WorldAdminQuery filter, CancellationToken token)
    {
        var query = dbContext.ResourceDeposits.AsNoTracking().Where(value => value.WorldId == world.Id);
        if (filter.RegionX is { } x && filter.RegionY is { } y)
            query = query.Where(value => value.RegionX == x && value.RegionY == y);
        var total = await query.LongCountAsync(token);
        var page = await query.OrderBy(value => value.ResourceCode).ThenBy(value => value.Id)
            .Skip(Skip(filter)).Take(filter.PageSize).ToListAsync(token);
        return (page.Select(value => Entity(value.Id, "resource", value.ResourceCode,
            value.IsExhausted ? "Exhausted" : value.IsDiscovered ? "Discovered" : "Hidden",
            null, null, value.RegionX, value.RegionY, null, null, new Dictionary<string, string>
            {
                ["quantity"] = value.Quantity.ToString(CultureInfo.InvariantCulture),
                ["capacity"] = value.Capacity.ToString(CultureInfo.InvariantCulture),
                ["renewable"] = value.IsRenewable.ToString()
            })).ToArray(), total);
    }

    private async Task<(IReadOnlyList<WorldAdminEntityView>, long)> ListCitiesAsync(
        World world, WorldAdminQuery filter, CancellationToken token)
    {
        var query = dbContext.Cities.AsNoTracking().Where(value => value.WorldId == world.Id);
        if (filter.RegionX is { } x && filter.RegionY is { } y)
            query = query.Where(value => value.CenterX >= x * world.ChunkSize && value.CenterX < (x + 1) * world.ChunkSize &&
                value.CenterY >= y * world.ChunkSize && value.CenterY < (y + 1) * world.ChunkSize);
        if (filter.FactionId is { } factionId) query = query.Where(value => value.GoverningFactionId == factionId);
        var total = await query.LongCountAsync(token);
        var page = await query.OrderBy(value => value.Name).ThenBy(value => value.Id)
            .Skip(Skip(filter)).Take(filter.PageSize).ToListAsync(token);
        return (page.Select(value => Entity(value.Id, "city", value.Name, value.Status.ToString(),
            value.CenterX, value.CenterY, value.CenterX / world.ChunkSize, value.CenterY / world.ChunkSize,
            value.GoverningFactionId, $"/api/cities/{value.Id}", new Dictionary<string, string>
            {
                ["population"] = value.Population.ToString(CultureInfo.InvariantCulture),
                ["wealth"] = value.Wealth.ToString(CultureInfo.InvariantCulture),
                ["markets"] = value.ResourceMarkets.Count.ToString(CultureInfo.InvariantCulture)
            })).ToArray(), total);
    }

    private async Task<(IReadOnlyList<WorldAdminEntityView>, long)> ListFactionsAsync(
        World world, WorldAdminQuery filter, bool armiesOnly, CancellationToken token)
    {
        var query = dbContext.Factions.AsNoTracking().Where(value => value.WorldId == world.Id);
        if (armiesOnly) query = query.Where(value => value.Type == FactionType.Army);
        if (filter.FactionId is { } factionId) query = query.Where(value => value.Id == factionId);
        if (filter.RegionX is { } x && filter.RegionY is { } y)
        {
            var minX = x * world.ChunkSize;
            var minY = y * world.ChunkSize;
            var ids = dbContext.FactionTerritoryTiles.Where(tile => tile.WorldId == world.Id && tile.IsActive &&
                tile.X >= minX && tile.X < minX + world.ChunkSize && tile.Y >= minY && tile.Y < minY + world.ChunkSize)
                .Select(tile => tile.FactionId);
            query = query.Where(value => ids.Contains(value.Id));
        }
        var total = await query.LongCountAsync(token);
        var page = await query.OrderBy(value => value.Name).ThenBy(value => value.Id)
            .Skip(Skip(filter)).Take(filter.PageSize).ToListAsync(token);
        return (page.Select(value => Entity(value.Id, "faction", value.Name, value.Status.ToString(),
            null, null, null, null, value.Id, $"/api/factions/{value.Id}", new Dictionary<string, string>
            {
                ["type"] = value.Type.ToString(),
                ["members"] = value.MemberActorIds.Count.ToString(CultureInfo.InvariantCulture),
                ["cities"] = value.ControlledCityIds.Count.ToString(CultureInfo.InvariantCulture),
                ["territory"] = value.Territory.Count.ToString(CultureInfo.InvariantCulture),
                ["wealth"] = value.Wealth.ToString(CultureInfo.InvariantCulture),
                ["military"] = value.MilitaryPower.ToString(CultureInfo.InvariantCulture),
                ["relations"] = value.Relations.Count.ToString(CultureInfo.InvariantCulture),
                ["wars"] = value.Relations.Values.Count(relation => relation.Kind == FactionRelationKind.War)
                    .ToString(CultureInfo.InvariantCulture)
            })).ToArray(), total);
    }

    private static int Skip(WorldAdminQuery query) => checked((query.Page - 1) * query.PageSize);

    private static WorldAdminEntityView Entity(
        Guid id, string type, string name, string status, int? x, int? y, int? regionX, int? regionY,
        Guid? factionId, string? detailPath, IReadOnlyDictionary<string, string> metrics) =>
        new(id, type, name, status, x, y, regionX, regionY, factionId, detailPath, metrics);
}

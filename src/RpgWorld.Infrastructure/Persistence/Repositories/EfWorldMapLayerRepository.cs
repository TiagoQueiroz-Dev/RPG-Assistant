using Microsoft.EntityFrameworkCore;
using RpgWorld.Application.Worlds.Admin;
using RpgWorld.Domain.Actors;
using RpgWorld.Domain.Worlds.Factions;

namespace RpgWorld.Infrastructure.Persistence.Repositories;

public sealed class EfWorldMapLayerRepository(
    RpgWorldDbContext dbContext,
    TimeProvider timeProvider) : IWorldMapLayerRepository
{
    public async Task<WorldMapLayerView?> LoadAsync(
        WorldMapLayerQuery request,
        CancellationToken cancellationToken = default)
    {
        var world = await dbContext.Worlds.AsNoTracking().SingleOrDefaultAsync(
            value => value.Id == request.WorldId, cancellationToken);
        if (world is null) return null;
        var minX = request.MinX ?? 0;
        var minY = request.MinY ?? 0;
        var maxX = Math.Min(request.MaxX ?? world.Width - 1, world.Width - 1);
        var maxY = Math.Min(request.MaxY ?? world.Height - 1, world.Height - 1);
        var cells = request.Mode switch
        {
            WorldMapLayerMode.Normal => [],
            WorldMapLayerMode.Biome => await TileTextLayerAsync(world.Id, minX, minY, maxX, maxY, cancellationToken),
            WorldMapLayerMode.Temperature => await TileTemperatureLayerAsync(world.Id, minX, minY, maxX, maxY, cancellationToken),
            WorldMapLayerMode.Population => await CityLayerAsync(world.Id, minX, minY, maxX, maxY, true, cancellationToken),
            WorldMapLayerMode.Economy => await CityLayerAsync(world.Id, minX, minY, maxX, maxY, false, cancellationToken),
            WorldMapLayerMode.Resources => await ResourceLayerAsync(world.Id, minX, minY, maxX, maxY, cancellationToken),
            WorldMapLayerMode.Political or WorldMapLayerMode.Faction =>
                await FactionLayerAsync(world.Id, minX, minY, maxX, maxY, false, cancellationToken),
            WorldMapLayerMode.Military => await FactionLayerAsync(world.Id, minX, minY, maxX, maxY, true, cancellationToken),
            WorldMapLayerMode.Danger => await DangerLayerAsync(world.Id, minX, minY, maxX, maxY, cancellationToken),
            WorldMapLayerMode.Religion => [],
            _ => throw new ArgumentOutOfRangeException(nameof(request.Mode))
        };
        return new WorldMapLayerView(
            world.Id, request.Mode.ToString(), timeProvider.GetUtcNow(), cells, Legend(request.Mode, cells));
    }

    private async Task<IReadOnlyList<WorldMapLayerCell>> TileTextLayerAsync(
        Guid worldId, int minX, int minY, int maxX, int maxY, CancellationToken token)
    {
        var tiles = await dbContext.Tiles.AsNoTracking().Where(tile => tile.WorldId == worldId &&
            tile.X >= minX && tile.X <= maxX && tile.Y >= minY && tile.Y <= maxY)
            .Select(tile => new { tile.X, tile.Y, tile.BiomeCode }).ToListAsync(token);
        return tiles.Select(tile => new WorldMapLayerCell(
            tile.X, tile.Y, 1m, tile.BiomeCode, BiomeColor(tile.BiomeCode))).ToArray();
    }

    private async Task<IReadOnlyList<WorldMapLayerCell>> TileTemperatureLayerAsync(
        Guid worldId, int minX, int minY, int maxX, int maxY, CancellationToken token)
    {
        var tiles = await dbContext.Tiles.AsNoTracking().Where(tile => tile.WorldId == worldId &&
            tile.X >= minX && tile.X <= maxX && tile.Y >= minY && tile.Y <= maxY)
            .Select(tile => new { tile.X, tile.Y, tile.TemperatureCelsius }).ToListAsync(token);
        return tiles.Select(tile => new WorldMapLayerCell(tile.X, tile.Y,
            Math.Clamp((tile.TemperatureCelsius + 30m) / 80m, 0m, 1m),
            $"{tile.TemperatureCelsius:0.#} °C", tile.TemperatureCelsius < 10m ? "#4c8faf" : "#d65a45")).ToArray();
    }

    private async Task<IReadOnlyList<WorldMapLayerCell>> CityLayerAsync(
        Guid worldId, int minX, int minY, int maxX, int maxY, bool population, CancellationToken token)
    {
        var cities = await dbContext.Cities.AsNoTracking().Where(city => city.WorldId == worldId &&
            city.CenterX >= minX && city.CenterX <= maxX && city.CenterY >= minY && city.CenterY <= maxY)
            .ToListAsync(token);
        var maximum = Math.Max(1m, cities.Select(city => population ? city.Population : city.Wealth).DefaultIfEmpty().Max());
        return cities.Select(city => new WorldMapLayerCell(city.CenterX, city.CenterY,
            (population ? city.Population : city.Wealth) / maximum,
            population ? $"{city.Name}: {city.Population} inhabitants" : $"{city.Name}: {city.Wealth:0.##} wealth",
            population ? "#d0a657" : "#4de0ad", city.Id)).ToArray();
    }

    private async Task<IReadOnlyList<WorldMapLayerCell>> ResourceLayerAsync(
        Guid worldId, int minX, int minY, int maxX, int maxY, CancellationToken token)
    {
        var tileDeposits = await (from deposit in dbContext.ResourceDeposits.AsNoTracking()
            join tile in dbContext.Tiles.AsNoTracking() on deposit.TileId equals tile.Id
            where deposit.WorldId == worldId && tile.X >= minX && tile.X <= maxX && tile.Y >= minY && tile.Y <= maxY
            select new { deposit.Id, deposit.ResourceCode, deposit.Quantity, deposit.Capacity, tile.X, tile.Y })
            .ToListAsync(token);
        return tileDeposits.Select(value => new WorldMapLayerCell(value.X, value.Y,
            value.Capacity == 0m ? 0m : value.Quantity / value.Capacity,
            $"{value.ResourceCode}: {value.Quantity:0.##}/{value.Capacity:0.##}", "#4de0ad", value.Id)).ToArray();
    }

    private async Task<IReadOnlyList<WorldMapLayerCell>> FactionLayerAsync(
        Guid worldId, int minX, int minY, int maxX, int maxY, bool military, CancellationToken token)
    {
        var factions = await dbContext.Factions.AsNoTracking().Where(value => value.WorldId == worldId)
            .ToDictionaryAsync(value => value.Id, token);
        var maximum = Math.Max(1m, factions.Values.Select(value => value.MilitaryPower).DefaultIfEmpty().Max());
        var tiles = await dbContext.FactionTerritoryTiles.AsNoTracking().Where(tile => tile.WorldId == worldId && tile.IsActive &&
            tile.X >= minX && tile.X <= maxX && tile.Y >= minY && tile.Y <= maxY).ToListAsync(token);
        return tiles.Where(tile => factions.ContainsKey(tile.FactionId)).Select(tile =>
        {
            var faction = factions[tile.FactionId];
            return new WorldMapLayerCell(tile.X, tile.Y, military ? faction.MilitaryPower / maximum : 1m,
                military ? $"{faction.Name}: {faction.MilitaryPower:0.##} military" : faction.Name,
                FactionColor(faction.Id), faction.Id);
        }).ToArray();
    }

    private async Task<IReadOnlyList<WorldMapLayerCell>> DangerLayerAsync(
        Guid worldId, int minX, int minY, int maxX, int maxY, CancellationToken token)
    {
        var creatures = await dbContext.Actors.AsNoTracking().OfType<CreatureActor>().Where(actor => actor.WorldId == worldId &&
            actor.X >= minX && actor.X <= maxX && actor.Y >= minY && actor.Y <= maxY)
            .GroupBy(actor => new { actor.X, actor.Y }).Select(group => new { group.Key.X, group.Key.Y, Count = group.Count() })
            .ToListAsync(token);
        var maximum = Math.Max(1, creatures.Select(value => value.Count).DefaultIfEmpty().Max());
        return creatures.Select(value => new WorldMapLayerCell(value.X, value.Y, value.Count / (decimal)maximum,
            $"{value.Count} dangerous creatures", "#d65a45")).ToArray();
    }

    private static IReadOnlyList<WorldMapLayerLegendItem> Legend(
        WorldMapLayerMode mode, IReadOnlyList<WorldMapLayerCell> cells) => mode switch
    {
        WorldMapLayerMode.Normal => [new("Normal terrain", "#5e665f")],
        WorldMapLayerMode.Biome => cells.GroupBy(cell => new { cell.Label, cell.Color })
            .Select(group => new WorldMapLayerLegendItem(group.Key.Label, group.Key.Color)).OrderBy(value => value.Label).ToArray(),
        WorldMapLayerMode.Temperature => [new("Cold", "#4c8faf", -30m, 10m), new("Warm", "#d65a45", 10m, 50m)],
        WorldMapLayerMode.Population => [new("Population concentration", "#d0a657", 0m, 1m)],
        WorldMapLayerMode.Economy => [new("Wealth concentration", "#4de0ad", 0m, 1m)],
        WorldMapLayerMode.Resources => [new("Available resources", "#4de0ad", 0m, 1m)],
        WorldMapLayerMode.Military => [new("Military strength", "#d65a45", 0m, 1m)],
        WorldMapLayerMode.Danger => [new("Danger", "#d65a45", 0m, 1m)],
        WorldMapLayerMode.Religion => [new("No religious influence data", "#787878")],
        _ => [new("Faction territory", "#d0a657")]
    };

    private static string BiomeColor(string code) => code switch
    {
        "forest" => "#2f6b4f", "desert" => "#c6a45e", "grassland" => "#77965a",
        "mountain" => "#73777b", "swamp" => "#456b61", "snow" => "#d9e4e5",
        "ocean" => "#315f7d", "river" => "#4c8faf", "volcanic" => "#873f32",
        _ => "#5e665f"
    };

    private static string FactionColor(Guid id)
    {
        var bytes = id.ToByteArray();
        return $"#{64 + bytes[0] % 160:X2}{64 + bytes[1] % 160:X2}{64 + bytes[2] % 160:X2}";
    }
}

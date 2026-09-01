using Microsoft.EntityFrameworkCore;
using RpgWorld.Application.Worlds.Visibility;
using RpgWorld.Domain.Worlds.Cities;
using RpgWorld.Infrastructure.Persistence;

namespace RpgWorld.Api.WorldMaps;

public sealed class PlayerWorldMapProvider(
    RpgWorldDbContext dbContext,
    IPlayerVisibilityService visibilityService)
{
    public async Task<WorldMapView?> GetMapAsync(
        Guid worldId,
        Guid playerActorId,
        CancellationToken cancellationToken = default)
    {
        var visibility = await visibilityService.GetAsync(playerActorId, cancellationToken);
        if (visibility.WorldId != worldId) return null;
        var world = await dbContext.Worlds.AsNoTracking().SingleOrDefaultAsync(
            value => value.Id == worldId, cancellationToken);
        if (world is null) return null;

        var states = visibility.Tiles.ToDictionary(value => (value.X, value.Y), value => value.State);
        var xValues = states.Keys.Select(value => value.X).Distinct().ToArray();
        var yValues = states.Keys.Select(value => value.Y).Distinct().ToArray();
        var tiles = await dbContext.Tiles.AsNoTracking().Where(tile => tile.WorldId == worldId &&
            xValues.Contains(tile.X) && yValues.Contains(tile.Y)).OrderBy(tile => tile.Y).ThenBy(tile => tile.X)
            .ToArrayAsync(cancellationToken);
        tiles = tiles.Where(tile => states.ContainsKey((tile.X, tile.Y))).ToArray();
        var visibleTiles = tiles.Where(tile => states[(tile.X, tile.Y)] == "Visible").ToArray();
        var visibleTileIds = visibleTiles.Select(tile => tile.Id).ToArray();
        var resources = await dbContext.ResourceDeposits.AsNoTracking().Where(deposit =>
            deposit.WorldId == worldId && deposit.TileId != null && visibleTileIds.Contains(deposit.TileId.Value) &&
            deposit.IsDiscovered).ToDictionaryAsync(deposit => deposit.Id, cancellationToken);
        var visibleCoordinates = visibleTiles.Select(tile => (tile.X, tile.Y)).ToHashSet();
        var visibleX = visibleCoordinates.Select(value => value.X).Distinct().ToArray();
        var visibleY = visibleCoordinates.Select(value => value.Y).Distinct().ToArray();
        var visibleCities = await (from territory in dbContext.CityTerritoryTiles.AsNoTracking()
            join city in dbContext.Cities.AsNoTracking() on territory.CityId equals city.Id
            where territory.WorldId == worldId && territory.IsActive && city.Status != CityStatus.Destroyed &&
                visibleX.Contains(territory.X) && visibleY.Contains(territory.Y)
            select new { territory.X, territory.Y, CityId = city.Id, CityName = city.Name }).ToArrayAsync(cancellationToken);
        var cities = visibleCities.Where(value => visibleCoordinates.Contains((value.X, value.Y)))
            .ToDictionary(value => (value.X, value.Y));
        var chunks = await dbContext.Chunks.AsNoTracking().Where(value => value.WorldId == worldId)
            .OrderBy(value => value.CoordinateY).ThenBy(value => value.CoordinateX).ToArrayAsync(cancellationToken);
        var tilesByChunk = tiles.GroupBy(tile => (tile.X / world.ChunkSize, tile.Y / world.ChunkSize))
            .ToDictionary(group => group.Key, group => group.ToArray());
        var chunkViews = chunks.Where(chunk => tilesByChunk.ContainsKey((chunk.CoordinateX, chunk.CoordinateY)))
            .Select(chunk => new WorldMapChunkView(
                chunk.CoordinateX,
                chunk.CoordinateY,
                chunk.OriginX,
                chunk.OriginY,
                chunk.Width,
                chunk.Height,
                tilesByChunk[(chunk.CoordinateX, chunk.CoordinateY)].Select(tile =>
                {
                    var state = states[(tile.X, tile.Y)];
                    var visible = state == "Visible";
                    var deposit = visible && tile.ResourceDepositId is { } resourceId && resources.TryGetValue(resourceId, out var found)
                        ? found : null;
                    var city = visible ? cities.GetValueOrDefault((tile.X, tile.Y)) : null;
                    return new WorldMapTileView(
                        tile.X,
                        tile.Y,
                        tile.TerrainCode,
                        tile.BiomeCode,
                        tile.Elevation,
                        tile.BiomeClassificationOrigin.ToString(),
                        tile.BiomeClassificationConfidence,
                        visible && tile.StructureId is not null,
                        deposit is not null,
                        deposit?.ResourceCode,
                        deposit?.Quantity,
                        deposit?.IsExhausted == true,
                        city?.CityId,
                        city?.CityName,
                        state);
                }).ToArray())).ToArray();

        return new WorldMapView(world.Id, world.Name, world.Width, world.Height, world.ChunkSize, chunkViews);
    }
}

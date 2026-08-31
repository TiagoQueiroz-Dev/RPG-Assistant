using Microsoft.EntityFrameworkCore;
using RpgWorld.Infrastructure.Persistence;
using RpgWorld.Domain.Worlds.Cities;

namespace RpgWorld.Api.WorldMaps;

public sealed class PersistedWorldMapProvider(RpgWorldDbContext dbContext)
{
    public async Task<WorldMapView?> GetMapAsync(
        Guid worldId,
        CancellationToken cancellationToken = default)
    {
        var world = await dbContext.Worlds
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.Id == worldId, cancellationToken);

        if (world is null)
        {
            return null;
        }

        var chunks = await dbContext.Chunks
            .AsNoTracking()
            .Where(chunk => chunk.WorldId == worldId)
            .OrderBy(chunk => chunk.CoordinateY)
            .ThenBy(chunk => chunk.CoordinateX)
            .ToArrayAsync(cancellationToken);
        var tiles = await dbContext.Tiles
            .AsNoTracking()
            .Where(tile => tile.WorldId == worldId)
            .OrderBy(tile => tile.Y)
            .ThenBy(tile => tile.X)
            .ToArrayAsync(cancellationToken);
        var resources = await dbContext.ResourceDeposits
            .AsNoTracking()
            .Where(deposit => deposit.WorldId == worldId && deposit.TileId != null)
            .ToDictionaryAsync(deposit => deposit.Id, cancellationToken);
        var cityTerritories = await (
                from territory in dbContext.CityTerritoryTiles.AsNoTracking()
                join city in dbContext.Cities.AsNoTracking() on territory.CityId equals city.Id
                where territory.WorldId == worldId && territory.IsActive && city.Status != CityStatus.Destroyed
                select new { territory.X, territory.Y, CityId = city.Id, CityName = city.Name })
            .ToDictionaryAsync(item => (item.X, item.Y), cancellationToken);
        var tilesByChunk = tiles
            .GroupBy(tile => (X: tile.X / world.ChunkSize, Y: tile.Y / world.ChunkSize))
            .ToDictionary(group => group.Key, group => group.ToArray());

        var chunkViews = chunks.Select(chunk =>
        {
            var chunkTiles = tilesByChunk
                .GetValueOrDefault((chunk.CoordinateX, chunk.CoordinateY), [])
                .Select(tile =>
                {
                    var deposit = tile.ResourceDepositId is { } resourceId &&
                        resources.TryGetValue(resourceId, out var linkedDeposit)
                            ? linkedDeposit
                            : null;
                    var hasDeposit = deposit?.IsDiscovered == true;
                    cityTerritories.TryGetValue((tile.X, tile.Y), out var city);
                    return new WorldMapTileView(
                        tile.X,
                        tile.Y,
                        tile.TerrainCode,
                        tile.BiomeCode,
                        tile.Elevation,
                        tile.BiomeClassificationOrigin.ToString(),
                        tile.BiomeClassificationConfidence,
                        tile.StructureId is not null,
                        hasDeposit,
                        hasDeposit ? deposit?.ResourceCode : null,
                        hasDeposit ? deposit?.Quantity : null,
                        hasDeposit && deposit?.IsExhausted == true,
                        city?.CityId,
                        city?.CityName);
                })
                .ToArray();

            return new WorldMapChunkView(
                chunk.CoordinateX,
                chunk.CoordinateY,
                chunk.OriginX,
                chunk.OriginY,
                chunk.Width,
                chunk.Height,
                chunkTiles);
        }).ToArray();

        return new WorldMapView(
            world.Id,
            world.Name,
            world.Width,
            world.Height,
            world.ChunkSize,
            chunkViews);
    }
}

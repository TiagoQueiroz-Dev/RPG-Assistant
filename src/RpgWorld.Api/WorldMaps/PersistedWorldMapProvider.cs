using Microsoft.EntityFrameworkCore;
using RpgWorld.Infrastructure.Persistence;

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
        var tilesByChunk = tiles
            .GroupBy(tile => (X: tile.X / world.ChunkSize, Y: tile.Y / world.ChunkSize))
            .ToDictionary(group => group.Key, group => group.ToArray());

        var chunkViews = chunks.Select(chunk =>
        {
            var chunkTiles = tilesByChunk
                .GetValueOrDefault((chunk.CoordinateX, chunk.CoordinateY), [])
                .Select(tile => new WorldMapTileView(
                    tile.X,
                    tile.Y,
                    tile.TerrainCode,
                    tile.BiomeCode,
                    tile.Elevation,
                    tile.BiomeClassificationOrigin.ToString(),
                    tile.BiomeClassificationConfidence,
                    tile.StructureId is not null))
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

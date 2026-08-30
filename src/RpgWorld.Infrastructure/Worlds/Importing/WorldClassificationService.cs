using Microsoft.EntityFrameworkCore;
using RpgWorld.Application.Worlds.Importing;
using RpgWorld.Domain.Worlds.Definitions;
using RpgWorld.Infrastructure.Persistence;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace RpgWorld.Infrastructure.Worlds.Importing;

public sealed class WorldClassificationService(
    RpgWorldDbContext dbContext,
    IWorldDefinitionCatalog definitions,
    IMapRegionClassifier classifier) : IWorldClassificationService
{
    public async Task<WorldClassificationResult> ReprocessAsync(
        Guid worldId,
        CancellationToken cancellationToken = default)
    {
        var source = await dbContext.WorldMapSourceImages
            .AsNoTracking()
            .SingleOrDefaultAsync(image => image.WorldId == worldId, cancellationToken)
            ?? throw new KeyNotFoundException("The world has no imported source image.");
        var tiles = await dbContext.Tiles
            .Where(tile => tile.WorldId == worldId)
            .ToArrayAsync(cancellationToken);
        using var image = Image.Load<Rgba32>(source.Data);
        var automaticallyClassified = 0;
        var preservedManual = 0;

        foreach (var tile in tiles)
        {
            var pixelX = Math.Min(image.Width - 1, tile.X * source.GridResolution + source.GridResolution / 2);
            var pixelY = Math.Min(image.Height - 1, tile.Y * source.GridResolution + source.GridResolution / 2);
            var pixel = image[pixelX, pixelY];
            var result = classifier.Classify(new MapColorSample(pixel.R, pixel.G, pixel.B));

            if (tile.ApplyAutomaticClassification(result.BiomeCode, definitions, result.Confidence))
            {
                automaticallyClassified++;
            }
            else
            {
                preservedManual++;
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return new WorldClassificationResult(
            worldId,
            automaticallyClassified,
            preservedManual,
            "completed");
    }

    public async Task ConfirmManualAsync(
        Guid worldId,
        int x,
        int y,
        string biomeCode,
        CancellationToken cancellationToken = default)
    {
        var tile = await dbContext.Tiles.SingleOrDefaultAsync(
            candidate => candidate.WorldId == worldId && candidate.X == x && candidate.Y == y,
            cancellationToken)
            ?? throw new KeyNotFoundException("Tile was not found.");
        tile.SetEnvironment(
            biomeCode,
            definitions,
            tile.Elevation,
            tile.TemperatureCelsius,
            tile.Humidity);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}

using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using RpgWorld.Application.Worlds.Importing;
using RpgWorld.Domain.Worlds;
using RpgWorld.Domain.Worlds.Definitions;
using RpgWorld.Infrastructure.Persistence;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.PixelFormats;

namespace RpgWorld.Infrastructure.Worlds.Importing;

public sealed class WorldImportService(
    RpgWorldDbContext dbContext,
    IWorldDefinitionCatalog definitions,
    IMapRegionClassifier classifier,
    TimeProvider timeProvider) : IWorldImportService
{
    public const int MaximumFileSize = 10 * 1024 * 1024;
    public const int MaximumImageDimension = 8192;
    public const int MaximumTileCount = 262_144;

    public async Task<WorldImportResult> ImportAsync(
        WorldImportRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateRequest(request);

        IImageFormat format;
        string mediaType;
        Image<Rgba32> image;

        try
        {
            format = Image.DetectFormat(request.ImageData);
            mediaType = ResolveMediaType(format);
            var imageInfo = Image.Identify(request.ImageData);
            ValidateDimensions(imageInfo.Width, imageInfo.Height, request.GridResolution);
            image = Image.Load<Rgba32>(request.ImageData);
        }
        catch (UnknownImageFormatException exception)
        {
            throw new WorldImportValidationException(
                $"The uploaded file is not a supported PNG, JPEG or WEBP image: {exception.Message}");
        }
        catch (InvalidImageContentException exception)
        {
            throw new WorldImportValidationException(
                $"The uploaded image is corrupted: {exception.Message}");
        }

        using (image)
        {
            var worldWidth = DivideRoundUp(image.Width, request.GridResolution);
            var worldHeight = DivideRoundUp(image.Height, request.GridResolution);
            var world = World.Create(request.Name, worldWidth, worldHeight);
            var chunks = CreateChunks(world);
            var tiles = CreateTiles(world, image, request.GridResolution);
            var sourceImage = new WorldMapSourceImage(
                world.Id,
                SafeFileName(request.FileName, format.FileExtensions.First()),
                mediaType,
                Convert.ToHexString(SHA256.HashData(request.ImageData)).ToLowerInvariant(),
                image.Width,
                image.Height,
                request.GridResolution,
                request.ImageData.ToArray(),
                timeProvider.GetUtcNow());

            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

            try
            {
                dbContext.Worlds.Add(world);
                dbContext.Chunks.AddRange(chunks);
                dbContext.Tiles.AddRange(tiles);
                dbContext.WorldMapSourceImages.Add(sourceImage);
                await dbContext.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
            }
            catch
            {
                await transaction.RollbackAsync(CancellationToken.None);
                dbContext.ChangeTracker.Clear();
                throw;
            }

            return new WorldImportResult(
                world.Id,
                world.Name,
                world.Width,
                world.Height,
                chunks.Count,
                tiles.Count,
                format.Name.ToLowerInvariant(),
                "completed");
        }
    }

    private static void ValidateRequest(WorldImportRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new WorldImportValidationException("World name is required.");
        }

        if (request.ImageData.Length is 0 or > MaximumFileSize)
        {
            throw new WorldImportValidationException(
                $"Image size must be between 1 byte and {MaximumFileSize / 1024 / 1024} MB.");
        }

        if (request.GridResolution is < 4 or > 512)
        {
            throw new WorldImportValidationException(
                "Grid resolution must be between 4 and 512 pixels per tile.");
        }
    }

    private static void ValidateDimensions(int width, int height, int gridResolution)
    {
        if (width > MaximumImageDimension || height > MaximumImageDimension)
        {
            throw new WorldImportValidationException(
                $"Image dimensions cannot exceed {MaximumImageDimension}x{MaximumImageDimension} pixels.");
        }

        var tileCount = (long)DivideRoundUp(width, gridResolution) * DivideRoundUp(height, gridResolution);

        if (tileCount > MaximumTileCount)
        {
            throw new WorldImportValidationException(
                $"The selected grid would create {tileCount} tiles; the limit is {MaximumTileCount}.");
        }
    }

    private static List<Chunk> CreateChunks(World world)
    {
        var chunks = new List<Chunk>(world.ChunkColumns * world.ChunkRows);

        for (var y = 0; y < world.ChunkRows; y++)
        {
            for (var x = 0; x < world.ChunkColumns; x++)
            {
                chunks.Add(world.CreateChunk(new ChunkCoordinate(x, y)));
            }
        }

        return chunks;
    }

    private List<Tile> CreateTiles(
        World world,
        Image<Rgba32> image,
        int gridResolution)
    {
        var tiles = new List<Tile>(world.Width * world.Height);

        for (var y = 0; y < world.Height; y++)
        {
            for (var x = 0; x < world.Width; x++)
            {
                var pixelX = Math.Min(image.Width - 1, x * gridResolution + gridResolution / 2);
                var pixelY = Math.Min(image.Height - 1, y * gridResolution + gridResolution / 2);
                var pixel = image[pixelX, pixelY];
                var classification = classifier.Classify(new MapColorSample(pixel.R, pixel.G, pixel.B));
                var biomeCode = classification.BiomeCode;
                var luminance = (pixel.R * 299 + pixel.G * 587 + pixel.B * 114) / 1000;
                var elevation = (short)Math.Clamp((luminance - 96) * 2, short.MinValue, short.MaxValue);

                tiles.Add(world.CreateTile(
                    world.PositionAt(x, y),
                    biomeCode,
                    definitions,
                    elevation,
                    temperatureCelsius: BiomeTemperature(biomeCode),
                    humidity: BiomeHumidity(biomeCode),
                    classificationOrigin: BiomeClassificationOrigin.Automatic,
                    classificationConfidence: classification.Confidence));
            }
        }

        return tiles;
    }

    private static decimal BiomeTemperature(string biomeCode) => biomeCode switch
    {
        "snow" => -10m,
        "desert" => 32m,
        "volcanic" => 45m,
        _ => 18m
    };

    private static decimal BiomeHumidity(string biomeCode) => biomeCode switch
    {
        "desert" or "volcanic" => 0.15m,
        "forest" => 0.70m,
        "swamp" or "river" or "ocean" => 0.90m,
        _ => 0.50m
    };

    private static string ResolveMediaType(IImageFormat format) => format.Name.ToUpperInvariant() switch
    {
        "PNG" => "image/png",
        "JPEG" => "image/jpeg",
        "WEBP" => "image/webp",
        _ => throw new WorldImportValidationException(
            $"Image format '{format.Name}' is not supported. Use PNG, JPEG or WEBP.")
    };

    private static string SafeFileName(string fileName, string fallbackExtension)
    {
        var safe = Path.GetFileName(fileName).Trim();
        return string.IsNullOrWhiteSpace(safe)
            ? $"imported-map.{fallbackExtension}"
            : safe.Length <= 255 ? safe : safe[^255..];
    }

    private static int DivideRoundUp(int value, int divisor) =>
        ((value - 1) / divisor) + 1;
}

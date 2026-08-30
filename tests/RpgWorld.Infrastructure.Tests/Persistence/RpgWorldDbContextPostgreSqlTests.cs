using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RpgWorld.Domain.Worlds;
using RpgWorld.Domain.Worlds.Definitions;
using RpgWorld.Infrastructure.Persistence;
using RpgWorld.Infrastructure.Persistence.Repositories;
using RpgWorld.Infrastructure.Worlds.Importing;
using RpgWorld.Application.Worlds.Importing;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.PixelFormats;
using RpgWorld.Modules.Default.Worlds;
using Testcontainers.PostgreSql;

namespace RpgWorld.Infrastructure.Tests.Persistence;

public sealed class RpgWorldDbContextPostgreSqlTests : IAsyncLifetime
{
    private static readonly WorldDefinitionCatalog TestDefinitions = new(
        [new TerrainDefinition("plains", "Plains", 1m, true, false)],
        [new BiomeDefinition("temperate", "Temperate", "plains", -10m, 40m, 0.20m, 0.90m)]);

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("rpg_world_tests")
        .WithUsername("rpg_world_tests")
        .WithPassword(Guid.NewGuid().ToString("N"))
        .Build();

    public Task InitializeAsync() => _postgres.StartAsync();

    public Task DisposeAsync() => _postgres.DisposeAsync().AsTask();

    [Fact]
    public async Task Migration_can_write_and_read_a_checkpoint()
    {
        var options = new DbContextOptionsBuilder<RpgWorldDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .Options;

        await using var context = new RpgWorldDbContext(options);
        await context.Database.MigrateAsync();

        var checkpointId = Guid.NewGuid();
        using var metadata = JsonDocument.Parse("""{"source":"integration-test"}""");

        context.PersistenceCheckpoints.Add(new PersistenceCheckpoint(
            checkpointId,
            DateTimeOffset.UtcNow,
            PersistenceCheckpointStatus.Succeeded,
            metadata));

        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var stored = await context.PersistenceCheckpoints
            .SingleAsync(checkpoint => checkpoint.Id == checkpointId);

        Assert.Equal(PersistenceCheckpointStatus.Succeeded, stored.Status);
        Assert.Equal(
            "integration-test",
            stored.Metadata.RootElement.GetProperty("source").GetString());
    }

    [Fact]
    public async Task Spatial_entities_are_persisted_and_queryable_by_position_and_chunk()
    {
        var options = new DbContextOptionsBuilder<RpgWorldDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .Options;

        await using var context = new RpgWorldDbContext(options);
        await context.Database.MigrateAsync();

        var world = World.Create("Aster", width: 65, height: 33);
        var chunkCoordinate = new ChunkCoordinate(2, 1);
        var chunk = world.CreateChunk(chunkCoordinate);
        var position = world.PositionAt(64, 32);
        var tile = world.CreateTile(
            position,
            "temperate",
            TestDefinitions,
            elevation: 42,
            temperatureCelsius: 18.5m,
            humidity: 0.65m);
        var actorId = Guid.NewGuid();
        tile.AddOccupant(actorId);

        context.AddRange(world, chunk, tile);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var repository = new EfWorldMapRepository(context);
        var storedTile = await repository.GetTileAsync(position);
        var storedChunk = await repository.GetChunkAsync(world.Id, chunkCoordinate);
        var chunkTiles = await repository.GetTilesAsync(world.Id, chunkCoordinate);

        Assert.NotNull(storedTile);
        Assert.Equal(actorId, Assert.Single(storedTile.OccupantIds));
        Assert.Equal(chunk.Id, storedChunk?.Id);
        Assert.Equal(tile.Id, Assert.Single(chunkTiles).Id);
    }

    [Fact]
    public async Task Chunk_changes_are_persisted_and_released_before_reloading()
    {
        var options = new DbContextOptionsBuilder<RpgWorldDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .Options;

        await using var context = new RpgWorldDbContext(options);
        await context.Database.MigrateAsync();

        var world = World.Create("Aster", width: 32, height: 32);
        var coordinate = new ChunkCoordinate(0, 0);
        var chunk = world.CreateChunk(coordinate);
        var tile = world.CreateTile(
            world.PositionAt(0, 0),
            "temperate",
            TestDefinitions,
            elevation: 0,
            temperatureCelsius: 20m,
            humidity: 0.50m);
        context.AddRange(world, chunk, tile);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var repository = new EfWorldMapRepository(context);
        var loadedChunk = await repository.GetChunkAsync(world.Id, coordinate);
        var loadedTiles = await repository.GetTilesAsync(world.Id, coordinate);
        var structureId = Guid.NewGuid();
        Assert.NotNull(loadedChunk);
        Assert.Empty(context.ChangeTracker.Entries<Chunk>());
        Assert.Empty(context.ChangeTracker.Entries<Tile>());
        Assert.Single(loadedTiles).AssignStructure(structureId);

        await repository.PersistAndReleaseChunkAsync(loadedChunk, loadedTiles);

        Assert.Empty(context.ChangeTracker.Entries<Chunk>());
        Assert.Empty(context.ChangeTracker.Entries<Tile>());
        var reloaded = await repository.GetTileAsync(world.PositionAt(0, 0));
        Assert.Equal(structureId, reloaded?.StructureId);
    }

    [Theory]
    [InlineData("png")]
    [InlineData("jpeg")]
    [InlineData("webp")]
    public async Task Imports_supported_image_as_atomic_world_grid(string format)
    {
        var options = new DbContextOptionsBuilder<RpgWorldDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .Options;
        await using var context = new RpgWorldDbContext(options);
        await context.Database.MigrateAsync();
        var bytes = CreateImage(format);
        var importer = new WorldImportService(context, DefaultWorldDefinitions.Catalog, TimeProvider.System);

        var result = await importer.ImportAsync(new WorldImportRequest(
            $"Imported {format}",
            $"map.{format}",
            bytes,
            GridResolution: 32));

        Assert.Equal("completed", result.Status);
        Assert.Equal(2, result.Width);
        Assert.Equal(2, result.Height);
        Assert.Equal(1, result.ChunkCount);
        Assert.Equal(4, result.TileCount);
        Assert.Equal(4, await context.Tiles.CountAsync(tile => tile.WorldId == result.WorldId));
        var source = await context.WorldMapSourceImages.SingleAsync(image => image.WorldId == result.WorldId);
        Assert.Equal(bytes, source.Data);
        Assert.Equal(64, source.PixelWidth);
        Assert.Equal(32, source.GridResolution);
    }

    [Fact]
    public async Task Corrupted_import_does_not_leave_partial_world()
    {
        var options = new DbContextOptionsBuilder<RpgWorldDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .Options;
        await using var context = new RpgWorldDbContext(options);
        await context.Database.MigrateAsync();
        var worldsBefore = await context.Worlds.CountAsync();
        var importer = new WorldImportService(context, DefaultWorldDefinitions.Catalog, TimeProvider.System);

        await Assert.ThrowsAsync<WorldImportValidationException>(() =>
            importer.ImportAsync(new WorldImportRequest(
                "Broken",
                "broken.png",
                [1, 2, 3, 4, 5],
                GridResolution: 32)));

        Assert.Equal(worldsBefore, await context.Worlds.CountAsync());
    }

    private static byte[] CreateImage(string format)
    {
        using var image = new Image<Rgba32>(64, 64, new Rgba32(30, 150, 60));
        using var stream = new MemoryStream();
        IImageEncoder encoder = format switch
        {
            "png" => new PngEncoder(),
            "jpeg" => new JpegEncoder(),
            "webp" => new WebpEncoder(),
            _ => throw new ArgumentOutOfRangeException(nameof(format))
        };
        image.Save(stream, encoder);
        return stream.ToArray();
    }
}

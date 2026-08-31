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
using RpgWorld.Application.Worlds.Editing;
using RpgWorld.Application.Actors;
using RpgWorld.Domain.Actors;
using RpgWorld.Infrastructure.Worlds.Editing;
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
    public async Task Simulation_repository_returns_only_running_worlds_and_persists_control_state()
    {
        var options = new DbContextOptionsBuilder<RpgWorldDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .Options;
        await using var context = new RpgWorldDbContext(options);
        await context.Database.MigrateAsync();
        var running = World.Create("Running", 8, 8);
        var paused = World.Create("Paused", 8, 8);
        paused.PauseSimulation();
        context.Worlds.AddRange(running, paused);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        var repository = new EfWorldSimulationRepository(context);

        Assert.Equal([running.Id], await repository.ListRunningWorldIdsAsync());

        var loadedPaused = await repository.GetAsync(paused.Id);
        Assert.NotNull(loadedPaused);
        loadedPaused.StartSimulation();
        await repository.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var runningIds = await repository.ListRunningWorldIdsAsync();
        Assert.Equal(2, runningIds.Count);
        Assert.Contains(running.Id, runningIds);
        Assert.Contains(paused.Id, runningIds);
    }

    [Fact]
    public async Task Region_simulation_level_and_aggregate_survive_reload()
    {
        var options = new DbContextOptionsBuilder<RpgWorldDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .Options;
        await using var context = new RpgWorldDbContext(options);
        await context.Database.MigrateAsync();
        var world = World.Create("Aggregated", 32, 32);
        var chunk = world.CreateChunk(new ChunkCoordinate(0, 0));
        var aggregate = new RegionAggregateState(250, 91.25m, 44m, 73.5m);
        chunk.TransitionSimulationLevel(SimulationLevel.Regional, aggregate);
        context.AddRange(world, chunk);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var stored = await context.Chunks.SingleAsync(candidate => candidate.Id == chunk.Id);

        Assert.Equal(SimulationLevel.Regional, stored.SimulationLevel);
        Assert.Equal(aggregate, stored.GetAggregateState());
        Assert.False(stored.AllowsIndividualActions);
    }

    [Fact]
    public async Task Actor_hierarchy_and_flexible_state_are_persisted_and_queryable()
    {
        var options = new DbContextOptionsBuilder<RpgWorldDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .Options;
        await using var context = new RpgWorldDbContext(options);
        await context.Database.MigrateAsync();
        var world = World.Create("Living world", 16, 16);
        var now = new DateTimeOffset(2026, 8, 30, 12, 0, 0, TimeSpan.Zero);
        var player = PlayerActor.Create("Ayla", world, world.PositionAt(2, 3), now);
        var npc = NpcActor.Create("Smith", world, world.PositionAt(2, 3), now);
        var creature = CreatureActor.Create("Wolf", world, world.PositionAt(8, 9), now, 35);
        var factionId = Guid.NewGuid();
        npc.SetAttribute("crafting", 18, now);
        npc.AddInventory("iron-ingot", 4, now);
        npc.JoinFaction(factionId, now);
        npc.SetReputation(factionId, 40, now);
        npc.SetRelationship(player.Id, "customer", 15, now);
        context.AddRange(world, player, npc, creature);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        var repository = new EfActorRepository(context);

        var actors = await repository.ListByWorldAsync(world.Id);
        var colocated = await repository.ListAtPositionAsync(world.PositionAt(2, 3));
        var storedNpc = Assert.IsType<NpcActor>(await repository.GetAsync(npc.Id));

        Assert.Equal(3, actors.Count);
        Assert.Contains(actors, actor => actor is PlayerActor);
        Assert.Contains(actors, actor => actor is CreatureActor);
        Assert.Equal(2, colocated.Count);
        Assert.Equal(18, storedNpc.Attributes["crafting"]);
        Assert.Equal(4, Assert.Single(storedNpc.Inventory).Quantity);
        Assert.Equal(40, storedNpc.Reputation[factionId]);
        Assert.Equal(player.Id, Assert.Single(storedNpc.Relationships).ActorId);
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
        var importer = new WorldImportService(
            context,
            DefaultWorldDefinitions.Catalog,
            new ColorMapRegionClassifier(),
            TimeProvider.System);

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
        var importer = new WorldImportService(
            context,
            DefaultWorldDefinitions.Catalog,
            new ColorMapRegionClassifier(),
            TimeProvider.System);

        await Assert.ThrowsAsync<WorldImportValidationException>(() =>
            importer.ImportAsync(new WorldImportRequest(
                "Broken",
                "broken.png",
                [1, 2, 3, 4, 5],
                GridResolution: 32)));

        Assert.Equal(worldsBefore, await context.Worlds.CountAsync());
    }

    [Fact]
    public async Task Reprocessing_preserves_manual_biome_confirmations()
    {
        var options = new DbContextOptionsBuilder<RpgWorldDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .Options;
        await using var context = new RpgWorldDbContext(options);
        await context.Database.MigrateAsync();
        var classifier = new ColorMapRegionClassifier();
        var importer = new WorldImportService(
            context,
            DefaultWorldDefinitions.Catalog,
            classifier,
            TimeProvider.System);
        var imported = await importer.ImportAsync(new WorldImportRequest(
            "Reviewable world",
            "map.png",
            CreateImage("png"),
            GridResolution: 32));
        var service = new WorldClassificationService(
            context,
            DefaultWorldDefinitions.Catalog,
            classifier);

        await service.ConfirmManualAsync(imported.WorldId, 0, 0, "snow");
        var result = await service.ReprocessAsync(imported.WorldId);
        context.ChangeTracker.Clear();

        var tiles = await context.Tiles
            .AsNoTracking()
            .Where(tile => tile.WorldId == imported.WorldId)
            .OrderBy(tile => tile.Y)
            .ThenBy(tile => tile.X)
            .ToArrayAsync();
        Assert.Equal(3, result.AutomaticallyClassified);
        Assert.Equal(1, result.PreservedManual);
        Assert.Equal("snow", tiles[0].BiomeCode);
        Assert.Equal(BiomeClassificationOrigin.Manual, tiles[0].BiomeClassificationOrigin);
        Assert.All(tiles[1..], tile =>
        {
            Assert.Equal("forest", tile.BiomeCode);
            Assert.Equal(BiomeClassificationOrigin.Automatic, tile.BiomeClassificationOrigin);
            Assert.NotNull(tile.BiomeClassificationConfidence);
        });
    }

    [Fact]
    public async Task Paints_multiple_tiles_and_persists_undo_redo_history()
    {
        var options = new DbContextOptionsBuilder<RpgWorldDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .Options;
        await using var context = new RpgWorldDbContext(options);
        await context.Database.MigrateAsync();
        var classifier = new ColorMapRegionClassifier();
        var imported = await new WorldImportService(
            context,
            DefaultWorldDefinitions.Catalog,
            classifier,
            TimeProvider.System).ImportAsync(new WorldImportRequest(
                "Editable world",
                "map.png",
                CreateImage("png"),
                GridResolution: 32));
        var editor = new MapEditingService(
            context,
            DefaultWorldDefinitions.Catalog,
            TimeProvider.System);

        var painted = await editor.PaintAsync(
            imported.WorldId,
            new MapPaintRequest(MapBrushKind.Desert, CenterX: 0, CenterY: 0, Size: 2));
        Assert.Equal(4, painted.AffectedTiles);
        context.ChangeTracker.Clear();
        var paintedTiles = await context.Tiles
            .Where(tile => tile.WorldId == imported.WorldId)
            .ToArrayAsync();
        Assert.All(paintedTiles, tile =>
        {
            Assert.Equal("desert", tile.BiomeCode);
            Assert.Equal(BiomeClassificationOrigin.Manual, tile.BiomeClassificationOrigin);
        });

        await editor.UndoAsync(imported.WorldId);
        context.ChangeTracker.Clear();
        var undoneTiles = await context.Tiles
            .Where(tile => tile.WorldId == imported.WorldId)
            .ToArrayAsync();
        Assert.All(undoneTiles, tile => Assert.Equal("forest", tile.BiomeCode));

        await editor.RedoAsync(imported.WorldId);
        context.ChangeTracker.Clear();
        Assert.Equal(
            4,
            await context.Tiles.CountAsync(tile =>
                tile.WorldId == imported.WorldId &&
                tile.BiomeCode == "desert" &&
                tile.BiomeClassificationOrigin == BiomeClassificationOrigin.Manual));

        await editor.PaintAsync(
            imported.WorldId,
            new MapPaintRequest(MapBrushKind.City, CenterX: 0, CenterY: 0, Size: 1));
        context.ChangeTracker.Clear();
        Assert.NotNull((await context.Tiles.SingleAsync(tile =>
            tile.WorldId == imported.WorldId && tile.X == 0 && tile.Y == 0)).StructureId);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            editor.PaintAsync(
                imported.WorldId,
                new MapPaintRequest(MapBrushKind.Forest, 0, 0, Size: 17)));
    }

    [Fact]
    public async Task World_clock_survives_context_restart_with_current_instant()
    {
        var options = new DbContextOptionsBuilder<RpgWorldDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .Options;
        var initial = new DateTimeOffset(2030, 5, 10, 9, 0, 0, TimeSpan.Zero);
        var world = World.Create("Clockwork", 8, 8);

        await using (var firstContext = new RpgWorldDbContext(options))
        {
            await firstContext.Database.MigrateAsync();
            firstContext.Worlds.Add(world);
            firstContext.WorldClocks.Add(WorldClock.Create(
                world.Id,
                initial,
                initial,
                tickDuration: TimeSpan.FromHours(1),
                realTimeMultiplier: 3m));
            await firstContext.SaveChangesAsync();
        }

        await using (var secondContext = new RpgWorldDbContext(options))
        {
            var repository = new EfWorldClockRepository(secondContext);
            var clock = await repository.GetAsync(world.Id);
            Assert.NotNull(clock);
            clock.AdvanceTicks(2);
            await repository.SaveChangesAsync();
        }

        await using var restartedContext = new RpgWorldDbContext(options);
        var restored = await restartedContext.WorldClocks.AsNoTracking().SingleAsync(clock => clock.WorldId == world.Id);
        Assert.Equal(initial.AddHours(2), restored.CurrentInstant);
        Assert.Equal(TimeSpan.FromHours(1), restored.TickDuration);
        Assert.Equal(3m, restored.RealTimeMultiplier);
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

using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RpgWorld.Domain.Worlds;
using RpgWorld.Infrastructure.Persistence;
using RpgWorld.Infrastructure.Persistence.Repositories;
using Testcontainers.PostgreSql;

namespace RpgWorld.Infrastructure.Tests.Persistence;

public sealed class RpgWorldDbContextPostgreSqlTests : IAsyncLifetime
{
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
            "grassland",
            "temperate",
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
}

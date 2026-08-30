using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RpgWorld.Infrastructure.Persistence;
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
}


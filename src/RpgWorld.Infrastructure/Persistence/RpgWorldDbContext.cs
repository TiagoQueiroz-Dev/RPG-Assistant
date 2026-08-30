using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace RpgWorld.Infrastructure.Persistence;

public sealed class RpgWorldDbContext(DbContextOptions<RpgWorldDbContext> options)
    : DbContext(options)
{
    public const string DefaultSchema = "rpg_world";

    public DbSet<PersistenceCheckpoint> PersistenceCheckpoints =>
        Set<PersistenceCheckpoint>();

    protected override void ConfigureConventions(
        ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<Guid>()
            .HaveColumnType("uuid");

        configurationBuilder.Properties<DateTime>()
            .HaveColumnType("timestamp with time zone");

        configurationBuilder.Properties<DateTimeOffset>()
            .HaveColumnType("timestamp with time zone");

        configurationBuilder.Properties<Enum>()
            .HaveConversion<string>()
            .HaveMaxLength(64);

        configurationBuilder.Properties<JsonDocument>()
            .HaveColumnType("jsonb");
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(DefaultSchema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(RpgWorldDbContext).Assembly);

        base.OnModelCreating(modelBuilder);
    }
}


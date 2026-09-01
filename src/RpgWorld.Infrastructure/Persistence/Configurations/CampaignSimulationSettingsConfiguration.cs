using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RpgWorld.Domain.Worlds;

namespace RpgWorld.Infrastructure.Persistence.Configurations;

internal sealed class CampaignSimulationSettingsConfiguration : IEntityTypeConfiguration<CampaignSimulationSettings>
{
    public void Configure(EntityTypeBuilder<CampaignSimulationSettings> builder)
    {
        builder.ToTable("campaign_simulation_settings");
        builder.HasKey(value => value.WorldId);
        builder.Property(value => value.WorldId).HasColumnName("world_id");
        builder.Property(value => value.NPCDensity).HasColumnName("npc_density").HasPrecision(8, 3);
        builder.Property(value => value.CreatureSpawnRate).HasColumnName("creature_spawn_rate").HasPrecision(8, 3);
        builder.Property(value => value.WarFrequency).HasColumnName("war_frequency").HasPrecision(8, 3);
        builder.Property(value => value.EconomicDifficulty).HasColumnName("economic_difficulty").HasPrecision(8, 3);
        builder.Property(value => value.ResourceScarcity).HasColumnName("resource_scarcity").HasPrecision(8, 3);
        builder.Property(value => value.MigrationRate).HasColumnName("migration_rate").HasPrecision(8, 3);
        builder.Property(value => value.PopulationGrowth).HasColumnName("population_growth").HasPrecision(8, 3);
        builder.Property(value => value.SimulationSpeed).HasColumnName("simulation_speed").HasPrecision(8, 3);
        builder.Property(value => value.Version).HasColumnName("version");
        builder.Property(value => value.UpdatedAtUtc).HasColumnName("updated_at_utc");
        builder.HasOne<World>().WithOne().HasForeignKey<CampaignSimulationSettings>(value => value.WorldId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

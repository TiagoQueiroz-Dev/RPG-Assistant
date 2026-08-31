using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RpgWorld.Domain.Worlds;
using RpgWorld.Domain.Worlds.Cities;

namespace RpgWorld.Infrastructure.Persistence.Configurations;

internal sealed class CityConfiguration : IEntityTypeConfiguration<City>
{
    public void Configure(EntityTypeBuilder<City> builder)
    {
        builder.ToTable("cities", table =>
        {
            table.HasCheckConstraint("ck_cities_population", "population >= 0");
            table.HasCheckConstraint("ck_cities_wealth", "wealth >= 0");
            table.HasCheckConstraint("ck_cities_version", "version >= 0");
            table.HasCheckConstraint("ck_cities_economic_cycle_count", "economic_cycle_count >= 0");
            table.HasCheckConstraint(
                "ck_cities_destroyed_state",
                "(status = 'Destroyed' AND destroyed_at_utc IS NOT NULL AND population = 0) OR " +
                "(status <> 'Destroyed' AND destroyed_at_utc IS NULL)");
        });
        builder.HasKey(city => city.Id);
        builder.Property(city => city.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(city => city.WorldId).HasColumnName("world_id");
        builder.Property(city => city.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
        builder.Property(city => city.CenterX).HasColumnName("center_x");
        builder.Property(city => city.CenterY).HasColumnName("center_y");
        builder.Property(city => city.Population).HasColumnName("population");
        builder.Property(city => city.Wealth).HasColumnName("wealth").HasPrecision(18, 2);
        builder.Property(city => city.GoverningFactionId).HasColumnName("governing_faction_id");
        builder.Property(city => city.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(24);
        builder.Property(city => city.FoundedAtUtc).HasColumnName("founded_at_utc");
        builder.Property(city => city.UpdatedAtUtc).HasColumnName("updated_at_utc");
        builder.Property(city => city.DestroyedAtUtc).HasColumnName("destroyed_at_utc");
        builder.Property(city => city.Version).HasColumnName("version").IsConcurrencyToken();
        builder.Property(city => city.EconomicCycleCount).HasColumnName("economic_cycle_count");
        builder.Property(city => city.LastEconomicCycleAtUtc).HasColumnName("last_economic_cycle_at_utc");
        ActorConfiguration.ConfigureJson(builder.Property<List<Guid>>("_residentActorIds"), "resident_actor_ids");
        ActorConfiguration.ConfigureJson(builder.Property<List<Guid>>("_buildingIds"), "building_ids");
        ActorConfiguration.ConfigureJson(builder.Property<Dictionary<string, decimal>>("_resourceStocks"), "resource_stocks");
        var resourceMarkets = builder.Property<Dictionary<string, CityResourceMarketSnapshot>>("_resourceMarkets");
        ActorConfiguration.ConfigureJson(resourceMarkets, "resource_markets");
        resourceMarkets.HasDefaultValueSql("'{}'::jsonb");
        ActorConfiguration.ConfigureJson(builder.Property<List<CityHistoryEntry>>("_history"), "history");
        builder.Ignore(city => city.Center);
        builder.Ignore(city => city.TerritoryTiles);
        builder.Ignore(city => city.Territory);
        builder.Ignore(city => city.ResidentActorIds);
        builder.Ignore(city => city.BuildingIds);
        builder.Ignore(city => city.ResourceStocks);
        builder.Ignore(city => city.ResourceMarkets);
        builder.Ignore(city => city.History);
        builder.Ignore(city => city.DomainEvents);
        builder.HasMany<CityTerritoryTile>("_territoryTiles")
            .WithOne()
            .HasForeignKey(tile => tile.CityId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation("_territoryTiles").UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.HasIndex(city => new { city.WorldId, city.Name })
            .IsUnique().HasDatabaseName("ux_cities_world_name");
        builder.HasIndex(city => new { city.WorldId, city.Status })
            .HasDatabaseName("ix_cities_world_status");
        builder.HasIndex(city => city.GoverningFactionId)
            .HasDatabaseName("ix_cities_governing_faction");
        builder.HasOne<World>().WithMany().HasForeignKey(city => city.WorldId).OnDelete(DeleteBehavior.Cascade);
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RpgWorld.Domain.Worlds.Cities;

namespace RpgWorld.Infrastructure.Persistence.Configurations;

internal sealed class CityTerritoryTileConfiguration : IEntityTypeConfiguration<CityTerritoryTile>
{
    public void Configure(EntityTypeBuilder<CityTerritoryTile> builder)
    {
        builder.ToTable("city_territory_tiles", table =>
        {
            table.HasCheckConstraint("ck_city_territory_tiles_x", "x >= 0");
            table.HasCheckConstraint("ck_city_territory_tiles_y", "y >= 0");
            table.HasCheckConstraint(
                "ck_city_territory_tiles_active",
                "(is_active AND released_at_utc IS NULL) OR (NOT is_active AND released_at_utc IS NOT NULL)");
        });
        builder.HasKey(tile => tile.Id);
        builder.Property(tile => tile.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(tile => tile.CityId).HasColumnName("city_id");
        builder.Property(tile => tile.WorldId).HasColumnName("world_id");
        builder.Property(tile => tile.X).HasColumnName("x");
        builder.Property(tile => tile.Y).HasColumnName("y");
        builder.Property(tile => tile.IsActive).HasColumnName("is_active");
        builder.Property(tile => tile.ReleasedAtUtc).HasColumnName("released_at_utc");
        builder.Ignore(tile => tile.Position);
        builder.HasIndex(tile => new { tile.WorldId, tile.X, tile.Y })
            .IsUnique().HasFilter("is_active")
            .HasDatabaseName("ux_city_territory_world_active_position");
        builder.HasIndex(tile => tile.CityId).HasDatabaseName("ix_city_territory_city");
    }
}

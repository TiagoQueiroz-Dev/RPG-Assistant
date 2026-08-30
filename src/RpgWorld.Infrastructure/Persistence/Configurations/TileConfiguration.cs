using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RpgWorld.Domain.Worlds;

namespace RpgWorld.Infrastructure.Persistence.Configurations;

internal sealed class TileConfiguration : IEntityTypeConfiguration<Tile>
{
    public void Configure(EntityTypeBuilder<Tile> builder)
    {
        builder.ToTable("tiles");
        builder.HasKey(tile => tile.Id);

        builder.Property(tile => tile.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();
        builder.Property(tile => tile.WorldId).HasColumnName("world_id");
        builder.Property(tile => tile.X).HasColumnName("x");
        builder.Property(tile => tile.Y).HasColumnName("y");
        builder.Property(tile => tile.TerrainCode)
            .HasColumnName("terrain_code")
            .HasMaxLength(80)
            .IsRequired();
        builder.Property(tile => tile.BiomeCode)
            .HasColumnName("biome_code")
            .HasMaxLength(80)
            .IsRequired();
        builder.Property(tile => tile.Elevation).HasColumnName("elevation");
        builder.Property(tile => tile.TemperatureCelsius)
            .HasColumnName("temperature_celsius")
            .HasPrecision(6, 2);
        builder.Property(tile => tile.Humidity)
            .HasColumnName("humidity")
            .HasPrecision(4, 3);
        builder.Property(tile => tile.ResourceDepositId)
            .HasColumnName("resource_deposit_id");
        builder.Property(tile => tile.StructureId)
            .HasColumnName("structure_id");

        builder.Ignore(tile => tile.Position);
        builder.Ignore(tile => tile.OccupantIds);
        builder.Property<Guid[]>("_occupantIds")
            .HasColumnName("occupant_ids")
            .HasColumnType("uuid[]")
            .IsRequired()
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(tile => new { tile.WorldId, tile.X, tile.Y })
            .IsUnique()
            .HasDatabaseName("ux_tiles_world_position");

        builder.HasOne<World>()
            .WithMany()
            .HasForeignKey(tile => tile.WorldId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}


using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RpgWorld.Domain.Worlds;

namespace RpgWorld.Infrastructure.Persistence.Configurations;

internal sealed class WorldMapSourceImageConfiguration
    : IEntityTypeConfiguration<WorldMapSourceImage>
{
    public void Configure(EntityTypeBuilder<WorldMapSourceImage> builder)
    {
        builder.ToTable("world_map_source_images");
        builder.HasKey(image => image.Id);
        builder.Property(image => image.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(image => image.WorldId).HasColumnName("world_id");
        builder.Property(image => image.FileName).HasColumnName("file_name").HasMaxLength(255).IsRequired();
        builder.Property(image => image.MediaType).HasColumnName("media_type").HasMaxLength(40).IsRequired();
        builder.Property(image => image.Sha256).HasColumnName("sha256").HasMaxLength(64).IsRequired();
        builder.Property(image => image.PixelWidth).HasColumnName("pixel_width");
        builder.Property(image => image.PixelHeight).HasColumnName("pixel_height");
        builder.Property(image => image.GridResolution).HasColumnName("grid_resolution");
        builder.Property(image => image.Data).HasColumnName("data").HasColumnType("bytea").IsRequired();
        builder.Property(image => image.ImportedAtUtc).HasColumnName("imported_at_utc");
        builder.HasIndex(image => image.WorldId).IsUnique().HasDatabaseName("ux_world_source_image_world");
        builder.HasIndex(image => image.Sha256).HasDatabaseName("ix_world_source_image_sha256");
        builder.HasOne<World>().WithOne().HasForeignKey<WorldMapSourceImage>(image => image.WorldId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

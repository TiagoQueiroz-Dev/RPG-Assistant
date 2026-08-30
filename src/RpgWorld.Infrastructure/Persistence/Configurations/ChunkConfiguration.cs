using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RpgWorld.Domain.Worlds;

namespace RpgWorld.Infrastructure.Persistence.Configurations;

internal sealed class ChunkConfiguration : IEntityTypeConfiguration<Chunk>
{
    public void Configure(EntityTypeBuilder<Chunk> builder)
    {
        builder.ToTable("chunks");
        builder.HasKey(chunk => chunk.Id);

        builder.Property(chunk => chunk.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();
        builder.Property(chunk => chunk.WorldId).HasColumnName("world_id");
        builder.Property(chunk => chunk.CoordinateX).HasColumnName("coordinate_x");
        builder.Property(chunk => chunk.CoordinateY).HasColumnName("coordinate_y");
        builder.Property(chunk => chunk.OriginX).HasColumnName("origin_x");
        builder.Property(chunk => chunk.OriginY).HasColumnName("origin_y");
        builder.Property(chunk => chunk.Width).HasColumnName("width");
        builder.Property(chunk => chunk.Height).HasColumnName("height");

        builder.Ignore(chunk => chunk.Coordinate);

        builder.HasIndex(chunk => new
            {
                chunk.WorldId,
                chunk.CoordinateX,
                chunk.CoordinateY
            })
            .IsUnique()
            .HasDatabaseName("ux_chunks_world_coordinate");

        builder.HasOne<World>()
            .WithMany()
            .HasForeignKey(chunk => chunk.WorldId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}


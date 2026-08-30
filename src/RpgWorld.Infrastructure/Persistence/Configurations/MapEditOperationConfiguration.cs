using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RpgWorld.Domain.Worlds;

namespace RpgWorld.Infrastructure.Persistence.Configurations;

internal sealed class MapEditOperationConfiguration : IEntityTypeConfiguration<MapEditOperation>
{
    public void Configure(EntityTypeBuilder<MapEditOperation> builder)
    {
        builder.ToTable("map_edit_operations");
        builder.HasKey(operation => operation.Id);
        builder.Property(operation => operation.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(operation => operation.WorldId).HasColumnName("world_id");
        builder.Property(operation => operation.Brush).HasColumnName("brush").HasConversion<string>().HasMaxLength(30);
        builder.Property(operation => operation.Changes).HasColumnName("changes").HasColumnType("jsonb");
        builder.Property(operation => operation.AffectedTiles).HasColumnName("affected_tiles");
        builder.Property(operation => operation.CreatedAtUtc).HasColumnName("created_at_utc");
        builder.Property(operation => operation.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(20);
        builder.HasIndex(operation => new { operation.WorldId, operation.CreatedAtUtc })
            .HasDatabaseName("ix_map_edits_world_created");
        builder.HasOne<World>().WithMany().HasForeignKey(operation => operation.WorldId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RpgWorld.Domain.Worlds;
using RpgWorld.Domain.Worlds.Events;

namespace RpgWorld.Infrastructure.Persistence.Configurations;

internal sealed class WorldConsequenceConfiguration : IEntityTypeConfiguration<WorldConsequence>
{
    public void Configure(EntityTypeBuilder<WorldConsequence> builder)
    {
        builder.ToTable("world_consequences", table =>
            table.HasCheckConstraint("ck_world_consequences_magnitude", "magnitude >= -100 AND magnitude <= 100"));
        builder.HasKey(value => value.Id);
        builder.Property(value => value.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(value => value.WorldId).HasColumnName("world_id");
        builder.Property(value => value.Kind).HasColumnName("kind").HasConversion<string>().HasMaxLength(32);
        builder.Property(value => value.TargetId).HasColumnName("target_id");
        builder.Property(value => value.Magnitude).HasColumnName("magnitude").HasPrecision(8, 2);
        builder.Property(value => value.Description).HasColumnName("description").HasMaxLength(500);
        builder.Property(value => value.SourceEventId).HasColumnName("source_event_id");
        builder.Property(value => value.OccurredAtUtc).HasColumnName("occurred_at_utc");
        builder.Ignore(value => value.DomainEvents);
        builder.HasOne<World>().WithMany().HasForeignKey(value => value.WorldId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(value => new { value.SourceEventId, value.Kind, value.TargetId })
            .IsUnique().HasDatabaseName("ux_world_consequences_source_kind_target");
        builder.HasIndex(value => new { value.WorldId, value.OccurredAtUtc })
            .HasDatabaseName("ix_world_consequences_world_time");
    }
}

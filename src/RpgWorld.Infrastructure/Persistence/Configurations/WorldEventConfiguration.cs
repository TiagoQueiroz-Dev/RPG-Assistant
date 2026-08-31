using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RpgWorld.Domain.Worlds;
using RpgWorld.Domain.Worlds.Events;

namespace RpgWorld.Infrastructure.Persistence.Configurations;

internal sealed class WorldEventConfiguration : IEntityTypeConfiguration<WorldEvent>
{
    public void Configure(EntityTypeBuilder<WorldEvent> builder)
    {
        builder.ToTable("world_events", table =>
        {
            table.HasCheckConstraint("ck_world_events_position",
                "(position_x IS NULL AND position_y IS NULL) OR " +
                "(position_x IS NOT NULL AND position_y IS NOT NULL AND position_x >= 0 AND position_y >= 0)");
            table.HasCheckConstraint("ck_world_events_payload_version", "payload_version > 0");
        });
        builder.HasKey(worldEvent => worldEvent.Id);
        builder.Property(worldEvent => worldEvent.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(worldEvent => worldEvent.WorldId).HasColumnName("world_id");
        builder.Property(worldEvent => worldEvent.Type).HasColumnName("type").HasMaxLength(160).IsRequired();
        builder.Property(worldEvent => worldEvent.TimestampUtc).HasColumnName("timestamp_utc");
        builder.Property(worldEvent => worldEvent.PositionX).HasColumnName("position_x");
        builder.Property(worldEvent => worldEvent.PositionY).HasColumnName("position_y");
        builder.Property<List<Guid>>("_actorIds").HasColumnName("actor_ids").HasColumnType("uuid[]");
        builder.Property(worldEvent => worldEvent.Payload).HasColumnName("payload").HasColumnType("jsonb").IsRequired();
        builder.Property(worldEvent => worldEvent.PayloadVersion).HasColumnName("payload_version");
        builder.Ignore(worldEvent => worldEvent.Position);
        builder.Ignore(worldEvent => worldEvent.ActorIds);
        builder.HasOne<World>().WithMany().HasForeignKey(worldEvent => worldEvent.WorldId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(worldEvent => new { worldEvent.WorldId, worldEvent.TimestampUtc, worldEvent.Id })
            .HasDatabaseName("ix_world_events_world_timeline");
        builder.HasIndex(worldEvent => new { worldEvent.WorldId, worldEvent.Type, worldEvent.TimestampUtc })
            .HasDatabaseName("ix_world_events_world_type_time");
        builder.HasIndex(worldEvent => new { worldEvent.WorldId, worldEvent.PositionX, worldEvent.PositionY })
            .HasDatabaseName("ix_world_events_world_position");
        builder.HasIndex("_actorIds").HasMethod("gin").HasDatabaseName("ix_world_events_actor_ids");
    }
}

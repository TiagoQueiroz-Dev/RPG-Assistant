using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RpgWorld.Domain.Actors;
using RpgWorld.Domain.Actors.Memories;

namespace RpgWorld.Infrastructure.Persistence.Configurations;

internal sealed class NpcMemoryConfiguration : IEntityTypeConfiguration<NpcMemory>
{
    public void Configure(EntityTypeBuilder<NpcMemory> builder)
    {
        builder.ToTable("npc_memories", table => table.HasCheckConstraint(
            "ck_npc_memories_importance",
            "importance BETWEEN 1 AND 100"));
        builder.HasKey(memory => memory.Id);
        builder.Property(memory => memory.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(memory => memory.ActorId).HasColumnName("actor_id");
        builder.Property(memory => memory.WorldId).HasColumnName("world_id");
        builder.Property(memory => memory.EventType).HasColumnName("event_type").HasMaxLength(80);
        builder.Property(memory => memory.TargetId).HasColumnName("target_id");
        builder.Property(memory => memory.Importance).HasColumnName("importance");
        builder.Property(memory => memory.CreatedAt).HasColumnName("created_at");
        builder.Property(memory => memory.ExpiresAt).HasColumnName("expires_at");
        ActorConfiguration.ConfigureJson(builder.Property<Dictionary<string, string>>("_payload"), "payload");
        builder.Ignore(memory => memory.Payload);
        builder.HasIndex(memory => new { memory.ActorId, memory.CreatedAt })
            .IsDescending(false, true)
            .HasDatabaseName("ix_npc_memories_actor_created");
        builder.HasIndex(memory => new { memory.ActorId, memory.TargetId, memory.Importance })
            .IsDescending(false, false, true)
            .HasDatabaseName("ix_npc_memories_actor_target_importance");
        builder.HasIndex(memory => memory.ExpiresAt)
            .HasFilter("expires_at IS NOT NULL")
            .HasDatabaseName("ix_npc_memories_expiration");
        builder.HasOne<NpcActor>().WithMany().HasForeignKey(memory => memory.ActorId).OnDelete(DeleteBehavior.Cascade);
    }
}

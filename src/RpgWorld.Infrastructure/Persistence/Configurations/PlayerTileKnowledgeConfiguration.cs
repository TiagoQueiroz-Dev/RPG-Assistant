using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RpgWorld.Domain.Actors;
using RpgWorld.Domain.Worlds;

namespace RpgWorld.Infrastructure.Persistence.Configurations;

internal sealed class PlayerTileKnowledgeConfiguration : IEntityTypeConfiguration<PlayerTileKnowledge>
{
    public void Configure(EntityTypeBuilder<PlayerTileKnowledge> builder)
    {
        builder.ToTable("player_tile_knowledge", table =>
        {
            table.HasCheckConstraint("ck_player_tile_knowledge_position", "x >= 0 AND y >= 0");
            table.HasCheckConstraint("ck_player_tile_knowledge_historical_state", "historical_state IN ('Discovered', 'Known')");
            table.HasCheckConstraint("ck_player_tile_knowledge_version", "version >= 0");
        });
        builder.HasKey(value => value.Id);
        builder.Property(value => value.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(value => value.PlayerActorId).HasColumnName("player_actor_id");
        builder.Property(value => value.WorldId).HasColumnName("world_id");
        builder.Property(value => value.X).HasColumnName("x");
        builder.Property(value => value.Y).HasColumnName("y");
        builder.Property(value => value.HistoricalState).HasColumnName("historical_state")
            .HasConversion<string>().HasMaxLength(24);
        builder.Property(value => value.DiscoveredAtUtc).HasColumnName("discovered_at_utc");
        builder.Property(value => value.KnownAtUtc).HasColumnName("known_at_utc");
        builder.Property(value => value.LastVisibleAtUtc).HasColumnName("last_visible_at_utc");
        builder.Property(value => value.Version).HasColumnName("version").IsConcurrencyToken();
        builder.Ignore(value => value.Position);
        builder.HasIndex(value => new { value.PlayerActorId, value.X, value.Y })
            .IsUnique().HasDatabaseName("ux_player_tile_knowledge_player_position");
        builder.HasIndex(value => new { value.PlayerActorId, value.HistoricalState })
            .HasDatabaseName("ix_player_tile_knowledge_player_state");
        builder.HasOne<PlayerActor>().WithMany().HasForeignKey(value => value.PlayerActorId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<World>().WithMany().HasForeignKey(value => value.WorldId).OnDelete(DeleteBehavior.Cascade);
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RpgWorld.Domain.Worlds;
using RpgWorld.Domain.Worlds.Factions;

namespace RpgWorld.Infrastructure.Persistence.Configurations;

internal sealed class FactionConfiguration : IEntityTypeConfiguration<Faction>
{
    public void Configure(EntityTypeBuilder<Faction> builder)
    {
        builder.ToTable("factions", table =>
        {
            table.HasCheckConstraint("ck_factions_wealth", "wealth >= 0");
            table.HasCheckConstraint("ck_factions_military_power", "military_power >= 0");
            table.HasCheckConstraint("ck_factions_version", "version >= 0");
            table.HasCheckConstraint(
                "ck_factions_dissolved_state",
                "(status = 'Dissolved' AND dissolved_at_utc IS NOT NULL AND leader_actor_id IS NULL) OR " +
                "(status = 'Active' AND dissolved_at_utc IS NULL AND leader_actor_id IS NOT NULL)");
        });
        builder.HasKey(faction => faction.Id);
        builder.Property(faction => faction.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(faction => faction.WorldId).HasColumnName("world_id");
        builder.Property(faction => faction.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
        builder.Property(faction => faction.Type).HasColumnName("type").HasConversion<string>().HasMaxLength(32);
        builder.Property(faction => faction.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(24);
        builder.Property(faction => faction.LeaderActorId).HasColumnName("leader_actor_id");
        builder.Property(faction => faction.Wealth).HasColumnName("wealth").HasPrecision(18, 2);
        builder.Property(faction => faction.MilitaryPower).HasColumnName("military_power").HasPrecision(18, 2);
        builder.Property(faction => faction.CreatedAtUtc).HasColumnName("created_at_utc");
        builder.Property(faction => faction.UpdatedAtUtc).HasColumnName("updated_at_utc");
        builder.Property(faction => faction.DissolvedAtUtc).HasColumnName("dissolved_at_utc");
        builder.Property(faction => faction.Version).HasColumnName("version").IsConcurrencyToken();
        ActorConfiguration.ConfigureJson(builder.Property<List<Guid>>("_memberActorIds"), "member_actor_ids");
        ActorConfiguration.ConfigureJson(builder.Property<List<Guid>>("_controlledCityIds"), "controlled_city_ids");
        ActorConfiguration.ConfigureJson(
            builder.Property<Dictionary<Guid, FactionRelation>>("_relations"), "relations");
        ActorConfiguration.ConfigureJson(builder.Property<List<FactionHistoryEntry>>("_history"), "history");
        builder.Ignore(faction => faction.MemberActorIds);
        builder.Ignore(faction => faction.ControlledCityIds);
        builder.Ignore(faction => faction.TerritoryTiles);
        builder.Ignore(faction => faction.Territory);
        builder.Ignore(faction => faction.Relations);
        builder.Ignore(faction => faction.History);
        builder.Ignore(faction => faction.DomainEvents);
        builder.HasMany<FactionTerritoryTile>("_territoryTiles")
            .WithOne().HasForeignKey(tile => tile.FactionId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation("_territoryTiles").UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.HasIndex(faction => new { faction.WorldId, faction.Name })
            .IsUnique().HasDatabaseName("ux_factions_world_name");
        builder.HasIndex(faction => new { faction.WorldId, faction.Status })
            .HasDatabaseName("ix_factions_world_status");
        builder.HasIndex(faction => faction.LeaderActorId).HasDatabaseName("ix_factions_leader");
        builder.HasOne<World>().WithMany().HasForeignKey(faction => faction.WorldId).OnDelete(DeleteBehavior.Cascade);
    }
}

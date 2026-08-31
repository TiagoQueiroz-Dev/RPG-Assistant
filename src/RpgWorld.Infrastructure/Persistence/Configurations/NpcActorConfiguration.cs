using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RpgWorld.Domain.Actors;

namespace RpgWorld.Infrastructure.Persistence.Configurations;

internal sealed class NpcActorConfiguration : IEntityTypeConfiguration<NpcActor>
{
    public void Configure(EntityTypeBuilder<NpcActor> builder)
    {
        builder.Property(npc => npc.Hunger).HasColumnName("hunger").HasPrecision(5, 2);
        builder.Property(npc => npc.Energy).HasColumnName("energy").HasPrecision(5, 2);
        builder.Property(npc => npc.Money).HasColumnName("money").HasPrecision(18, 2);
        builder.Property(npc => npc.Job).HasColumnName("job").HasMaxLength(120);
        builder.Property(npc => npc.HomeX).HasColumnName("home_x");
        builder.Property(npc => npc.HomeY).HasColumnName("home_y");
        builder.Property(npc => npc.NeedsUpdatedAt).HasColumnName("needs_updated_at");
        ActorConfiguration.ConfigureJson(builder.Property<List<Guid>>("_familyIds"), "family_ids");
        ActorConfiguration.ConfigureJson(builder.Property<List<NpcGoal>>("_goals"), "goals");
        ActorConfiguration.ConfigureJson(builder.Property<List<string>>("_traitCodes"), "trait_codes");
        builder.Ignore(npc => npc.Home);
        builder.Ignore(npc => npc.FamilyIds);
        builder.Ignore(npc => npc.Goals);
        builder.Ignore(npc => npc.TraitCodes);
        builder.HasIndex(npc => new { npc.WorldId, npc.Hunger })
            .IsDescending(false, true)
            .HasFilter("actor_type = 'npc' AND status <> 'Dead'")
            .HasDatabaseName("ix_actors_npc_hunger");
        builder.HasIndex(npc => new { npc.WorldId, npc.Energy })
            .HasFilter("actor_type = 'npc' AND status <> 'Dead'")
            .HasDatabaseName("ix_actors_npc_energy");
        builder.HasIndex(npc => new { npc.WorldId, npc.Job })
            .HasFilter("actor_type = 'npc'")
            .HasDatabaseName("ix_actors_npc_job");
    }
}

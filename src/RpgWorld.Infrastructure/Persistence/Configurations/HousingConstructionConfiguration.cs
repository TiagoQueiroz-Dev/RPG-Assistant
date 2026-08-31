using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RpgWorld.Domain.Actors;
using RpgWorld.Domain.Actors.Housing;
using RpgWorld.Domain.Worlds;

namespace RpgWorld.Infrastructure.Persistence.Configurations;

internal sealed class HousingConstructionConfiguration : IEntityTypeConfiguration<HousingConstruction>
{
    public void Configure(EntityTypeBuilder<HousingConstruction> builder)
    {
        builder.ToTable("housing_constructions", table => table.HasCheckConstraint(
            "ck_housing_constructions_progress", "progress BETWEEN 0 AND 100"));
        builder.HasKey(construction => construction.Id);
        builder.Property(construction => construction.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(construction => construction.WorldId).HasColumnName("world_id");
        builder.Property(construction => construction.OwnerActorId).HasColumnName("owner_actor_id");
        ActorConfiguration.ConfigureJson(builder.Property<List<Guid>>("_residentActorIds"), "resident_actor_ids");
        builder.Property(construction => construction.X).HasColumnName("x");
        builder.Property(construction => construction.Y).HasColumnName("y");
        builder.Property(construction => construction.RequiredWood).HasColumnName("required_wood");
        builder.Property(construction => construction.RequiredStone).HasColumnName("required_stone");
        builder.Property(construction => construction.ConsumedWood).HasColumnName("consumed_wood");
        builder.Property(construction => construction.ConsumedStone).HasColumnName("consumed_stone");
        builder.Property(construction => construction.Progress).HasColumnName("progress");
        builder.Property(construction => construction.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(24);
        builder.Property(construction => construction.CreatedAt).HasColumnName("created_at");
        builder.Property(construction => construction.UpdatedAt).HasColumnName("updated_at");
        builder.Property(construction => construction.CompletedAt).HasColumnName("completed_at");
        builder.Ignore(construction => construction.Position);
        builder.Ignore(construction => construction.ResidentActorIds);
        builder.HasIndex(construction => new { construction.WorldId, construction.X, construction.Y })
            .IsUnique().HasDatabaseName("ux_housing_constructions_world_position");
        builder.HasIndex(construction => new { construction.WorldId, construction.Status })
            .HasDatabaseName("ix_housing_constructions_world_status");
        builder.HasIndex(construction => construction.OwnerActorId)
            .HasFilter("status = 'InProgress'").IsUnique()
            .HasDatabaseName("ux_housing_constructions_active_owner");
        builder.HasOne<NpcActor>().WithMany().HasForeignKey(construction => construction.OwnerActorId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<World>().WithMany().HasForeignKey(construction => construction.WorldId).OnDelete(DeleteBehavior.Cascade);
    }
}

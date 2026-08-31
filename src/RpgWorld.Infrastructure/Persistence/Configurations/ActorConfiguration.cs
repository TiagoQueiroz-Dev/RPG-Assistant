using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RpgWorld.Domain.Actors;
using RpgWorld.Domain.Worlds;

namespace RpgWorld.Infrastructure.Persistence.Configurations;

internal sealed class ActorConfiguration : IEntityTypeConfiguration<Actor>
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public void Configure(EntityTypeBuilder<Actor> builder)
    {
        builder.ToTable("actors");
        builder.HasKey(actor => actor.Id);
        builder.Property(actor => actor.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(actor => actor.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
        builder.Property(actor => actor.WorldId).HasColumnName("world_id");
        builder.Property(actor => actor.X).HasColumnName("x");
        builder.Property(actor => actor.Y).HasColumnName("y");
        builder.Property(actor => actor.Health).HasColumnName("health");
        builder.Property(actor => actor.MaximumHealth).HasColumnName("maximum_health");
        builder.Property(actor => actor.FactionId).HasColumnName("faction_id");
        builder.Property(actor => actor.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(32);
        builder.Property(actor => actor.CurrentAction).HasColumnName("current_action").HasMaxLength(120);
        builder.Property(actor => actor.CreatedAtUtc).HasColumnName("created_at_utc");
        builder.Property(actor => actor.UpdatedAtUtc).HasColumnName("updated_at_utc");

        ConfigureJson(builder.Property<Dictionary<string, int>>("_attributes"), "attributes");
        ConfigureJson(builder.Property<List<InventoryItem>>("_inventory"), "inventory");
        ConfigureJson(builder.Property<Dictionary<Guid, int>>("_reputation"), "reputation");
        ConfigureJson(builder.Property<List<ActorRelationship>>("_relationships"), "relationships");

        builder.Ignore(actor => actor.Position);
        builder.Ignore(actor => actor.Attributes);
        builder.Ignore(actor => actor.Inventory);
        builder.Ignore(actor => actor.Reputation);
        builder.Ignore(actor => actor.Relationships);
        builder.Ignore(actor => actor.Kind);
        builder.Ignore(actor => actor.DomainEvents);

        builder.HasDiscriminator<string>("actor_type")
            .HasValue<PlayerActor>("player")
            .HasValue<NpcActor>("npc")
            .HasValue<CreatureActor>("creature");
        builder.Property<string>("actor_type").HasColumnName("actor_type").HasMaxLength(20);

        builder.HasIndex(actor => new { actor.WorldId, actor.X, actor.Y })
            .HasDatabaseName("ix_actors_world_position");
        builder.HasIndex(actor => new { actor.WorldId, actor.Status })
            .HasDatabaseName("ix_actors_world_status");
        builder.HasOne<World>().WithMany().HasForeignKey(actor => actor.WorldId).OnDelete(DeleteBehavior.Cascade);
    }

    private static void ConfigureJson<T>(PropertyBuilder<T> property, string columnName)
        where T : class
    {
        property.HasColumnName(columnName)
            .HasColumnType("jsonb")
            .HasConversion(
                value => Serialize(value),
                json => Deserialize<T>(json));
        property.Metadata.SetValueComparer(new ValueComparer<T>(
            (left, right) => Serialize(left) == Serialize(right),
            value => Serialize(value).GetHashCode(StringComparison.Ordinal),
            value => Deserialize<T>(Serialize(value))));
    }

    private static string Serialize<T>(T? value) => JsonSerializer.Serialize(value, SerializerOptions);

    private static T Deserialize<T>(string json) =>
        JsonSerializer.Deserialize<T>(json, SerializerOptions)
        ?? throw new InvalidOperationException("Actor JSON state could not be deserialized.");
}

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RpgWorld.Domain.Worlds;
using RpgWorld.Domain.Worlds.Resources;

namespace RpgWorld.Infrastructure.Persistence.Configurations;

internal sealed class ResourceDepositConfiguration : IEntityTypeConfiguration<ResourceDeposit>
{
    public void Configure(EntityTypeBuilder<ResourceDeposit> builder)
    {
        builder.ToTable("resource_deposits", table =>
        {
            table.HasCheckConstraint("ck_resource_deposits_quantity", "quantity >= 0 AND capacity > 0 AND quantity <= capacity");
            table.HasCheckConstraint("ck_resource_deposits_regeneration", "regeneration_per_world_hour >= 0");
            table.HasCheckConstraint("ck_resource_deposits_version", "version >= 0");
            table.HasCheckConstraint(
                "ck_resource_deposits_location",
                "(scope = 'Tile' AND tile_id IS NOT NULL) OR (scope = 'Region' AND tile_id IS NULL)");
            table.HasCheckConstraint(
                "ck_resource_deposits_discovery",
                "(is_discovered AND discovered_by_actor_id IS NOT NULL AND discovered_at_utc IS NOT NULL) OR " +
                "(NOT is_discovered AND discovered_by_actor_id IS NULL AND discovered_at_utc IS NULL)");
        });
        builder.HasKey(deposit => deposit.Id);
        builder.Property(deposit => deposit.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(deposit => deposit.WorldId).HasColumnName("world_id");
        builder.Property(deposit => deposit.ResourceCode).HasColumnName("resource_code").HasMaxLength(80).IsRequired();
        builder.Property(deposit => deposit.InventoryItemCode).HasColumnName("inventory_item_code").HasMaxLength(80).IsRequired();
        builder.Property(deposit => deposit.Scope).HasColumnName("scope").HasConversion<string>().HasMaxLength(16);
        builder.Property(deposit => deposit.TileId).HasColumnName("tile_id");
        builder.Property(deposit => deposit.RegionX).HasColumnName("region_x");
        builder.Property(deposit => deposit.RegionY).HasColumnName("region_y");
        builder.Property(deposit => deposit.Quantity).HasColumnName("quantity").HasPrecision(18, 4);
        builder.Property(deposit => deposit.Capacity).HasColumnName("capacity").HasPrecision(18, 4);
        builder.Property(deposit => deposit.RegenerationPerWorldHour).HasColumnName("regeneration_per_world_hour").HasPrecision(18, 4);
        builder.Property(deposit => deposit.IsDiscovered).HasColumnName("is_discovered");
        builder.Property(deposit => deposit.DiscoveredByActorId).HasColumnName("discovered_by_actor_id");
        builder.Property(deposit => deposit.DiscoveredAtUtc).HasColumnName("discovered_at_utc");
        builder.Property(deposit => deposit.LastConsumerKind).HasColumnName("last_consumer_kind").HasConversion<string>().HasMaxLength(24);
        builder.Property(deposit => deposit.LastConsumerId).HasColumnName("last_consumer_id");
        builder.Property(deposit => deposit.SourceWorldEventId).HasColumnName("source_world_event_id");
        builder.Property(deposit => deposit.CreatedAtUtc).HasColumnName("created_at_utc");
        builder.Property(deposit => deposit.UpdatedAtUtc).HasColumnName("updated_at_utc");
        builder.Property(deposit => deposit.LastRegeneratedAtUtc).HasColumnName("last_regenerated_at_utc");
        builder.Property(deposit => deposit.Version).HasColumnName("version").IsConcurrencyToken();
        builder.Ignore(deposit => deposit.Region);
        builder.Ignore(deposit => deposit.IsRenewable);
        builder.Ignore(deposit => deposit.IsExhausted);
        builder.Ignore(deposit => deposit.DomainEvents);
        builder.HasIndex(deposit => deposit.TileId)
            .IsUnique().HasFilter("tile_id IS NOT NULL")
            .HasDatabaseName("ux_resource_deposits_tile");
        builder.HasIndex(deposit => new { deposit.WorldId, deposit.RegionX, deposit.RegionY, deposit.ResourceCode })
            .HasDatabaseName("ix_resource_deposits_world_region_resource");
        builder.HasIndex(deposit => new { deposit.WorldId, deposit.IsDiscovered, deposit.ResourceCode })
            .HasDatabaseName("ix_resource_deposits_world_discovered_resource");
        builder.HasOne<World>().WithMany().HasForeignKey(deposit => deposit.WorldId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Tile>().WithMany().HasForeignKey(deposit => deposit.TileId).OnDelete(DeleteBehavior.Restrict);
    }
}

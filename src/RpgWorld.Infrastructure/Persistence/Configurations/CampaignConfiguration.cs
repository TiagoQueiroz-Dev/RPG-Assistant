using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RpgWorld.Domain.Campaigns;
using RpgWorld.Domain.Worlds;

namespace RpgWorld.Infrastructure.Persistence.Configurations;

internal sealed class CampaignConfiguration : IEntityTypeConfiguration<Campaign>
{
    public void Configure(EntityTypeBuilder<Campaign> builder)
    {
        builder.ToTable("campaigns");
        builder.HasKey(value => value.Id);
        builder.Property(value => value.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(value => value.WorldId).HasColumnName("world_id");
        builder.Property(value => value.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
        builder.Property(value => value.ModuleId).HasColumnName("module_id").HasMaxLength(200).IsRequired();
        builder.Property(value => value.SettingsJson).HasColumnName("settings").HasColumnType("jsonb").IsRequired();
        builder.Property(value => value.Status).HasColumnName("status");
        builder.Property(value => value.CreatedAtUtc).HasColumnName("created_at_utc");
        builder.Property(value => value.EndedAtUtc).HasColumnName("ended_at_utc");
        builder.HasIndex(value => new { value.WorldId, value.CreatedAtUtc, value.Id });
        builder.HasOne<World>().WithMany().HasForeignKey(value => value.WorldId).OnDelete(DeleteBehavior.Restrict);
    }
}

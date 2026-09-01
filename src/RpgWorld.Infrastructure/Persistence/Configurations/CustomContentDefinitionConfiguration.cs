using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RpgWorld.Domain.Worlds.Content;

namespace RpgWorld.Infrastructure.Persistence.Configurations;

internal sealed class CustomContentDefinitionConfiguration : IEntityTypeConfiguration<CustomContentDefinition>
{
    public void Configure(EntityTypeBuilder<CustomContentDefinition> builder)
    {
        builder.ToTable("custom_content_definitions");
        builder.HasKey(value => value.Id);
        builder.Property(value => value.Id).HasColumnName("id");
        builder.Property(value => value.WorldId).HasColumnName("world_id").IsRequired();
        builder.Property(value => value.Kind).HasColumnName("kind").HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(value => value.Code).HasColumnName("code").HasMaxLength(80).IsRequired();
        builder.Property(value => value.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
        builder.Property(value => value.Payload).HasColumnName("payload").HasColumnType("jsonb").IsRequired();
        builder.Property(value => value.Version).HasColumnName("version").IsRequired();
        builder.Property(value => value.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();
        builder.Property(value => value.UpdatedAtUtc).HasColumnName("updated_at_utc").IsRequired();
        builder.HasIndex(value => new { value.WorldId, value.Kind, value.Code }).IsUnique();
        builder.HasOne<RpgWorld.Domain.Worlds.World>().WithMany().HasForeignKey(value => value.WorldId).OnDelete(DeleteBehavior.Cascade);
    }
}

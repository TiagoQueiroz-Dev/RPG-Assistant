using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace RpgWorld.Infrastructure.Persistence.Configurations;

internal sealed class PersistenceCheckpointConfiguration
    : IEntityTypeConfiguration<PersistenceCheckpoint>
{
    public void Configure(EntityTypeBuilder<PersistenceCheckpoint> builder)
    {
        builder.ToTable("persistence_checkpoints");

        builder.HasKey(checkpoint => checkpoint.Id);

        builder.Property(checkpoint => checkpoint.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(checkpoint => checkpoint.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .IsRequired();

        builder.Property(checkpoint => checkpoint.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(checkpoint => checkpoint.Metadata)
            .HasColumnName("metadata")
            .HasColumnType("jsonb")
            .IsRequired();

        builder.HasIndex(checkpoint => checkpoint.CreatedAtUtc)
            .HasDatabaseName("ix_persistence_checkpoints_created_at_utc");
    }
}


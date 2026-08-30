using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RpgWorld.Domain.Worlds;

namespace RpgWorld.Infrastructure.Persistence.Configurations;

internal sealed class WorldConfiguration : IEntityTypeConfiguration<World>
{
    public void Configure(EntityTypeBuilder<World> builder)
    {
        builder.ToTable("worlds");
        builder.HasKey(world => world.Id);

        builder.Property(world => world.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();
        builder.Property(world => world.Name)
            .HasColumnName("name")
            .HasMaxLength(200)
            .IsRequired();
        builder.Property(world => world.Width).HasColumnName("width");
        builder.Property(world => world.Height).HasColumnName("height");
        builder.Property(world => world.ChunkSize).HasColumnName("chunk_size");

        builder.Ignore(world => world.ChunkColumns);
        builder.Ignore(world => world.ChunkRows);
        builder.Ignore(world => world.DomainEvents);
    }
}


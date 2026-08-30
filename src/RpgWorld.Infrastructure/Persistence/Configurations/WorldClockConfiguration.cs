using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RpgWorld.Domain.Worlds;

namespace RpgWorld.Infrastructure.Persistence.Configurations;

internal sealed class WorldClockConfiguration : IEntityTypeConfiguration<WorldClock>
{
    public void Configure(EntityTypeBuilder<WorldClock> builder)
    {
        builder.ToTable("world_clocks");
        builder.HasKey(clock => clock.WorldId);
        builder.Property(clock => clock.WorldId).HasColumnName("world_id").ValueGeneratedNever();
        builder.Property(clock => clock.CurrentInstant).HasColumnName("current_instant");
        builder.Property(clock => clock.TickDuration).HasColumnName("tick_duration").HasColumnType("interval");
        builder.Property(clock => clock.RealTimeMultiplier)
            .HasColumnName("real_time_multiplier")
            .HasPrecision(10, 3);
        builder.Property(clock => clock.LastSynchronizedAtUtc).HasColumnName("last_synchronized_at_utc");
        builder.HasOne<World>().WithOne().HasForeignKey<WorldClock>(clock => clock.WorldId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

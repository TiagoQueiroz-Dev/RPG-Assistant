using RpgWorld.Domain.Worlds;

namespace RpgWorld.Domain.Tests.Worlds;

public sealed class WorldClockTests
{
    [Fact]
    public void Advances_by_an_exact_manual_duration()
    {
        var initial = new DateTimeOffset(2030, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var clock = WorldClock.Create(Guid.NewGuid(), initial, initial);

        clock.AdvanceBy(TimeSpan.FromDays(3.5));

        Assert.Equal(initial.AddDays(3.5), clock.CurrentInstant);
    }

    [Fact]
    public void Tick_advances_exactly_the_configured_duration()
    {
        var initial = new DateTimeOffset(2026, 1, 1, 8, 0, 0, TimeSpan.Zero);
        var clock = WorldClock.Create(
            Guid.NewGuid(),
            initial,
            initial,
            tickDuration: TimeSpan.FromMinutes(15));

        var elapsed = clock.AdvanceTicks(3);

        Assert.Equal(TimeSpan.FromMinutes(45), elapsed);
        Assert.Equal(initial.AddMinutes(45), clock.CurrentInstant);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(0.5, 15)]
    [InlineData(2, 60)]
    [InlineData(10, 300)]
    public void Converts_real_time_using_multiplier(double multiplier, int expectedWorldSeconds)
    {
        var now = DateTimeOffset.UtcNow;
        var clock = WorldClock.Create(Guid.NewGuid(), now, now, realTimeMultiplier: (decimal)multiplier);

        Assert.Equal(
            TimeSpan.FromSeconds(expectedWorldSeconds),
            clock.ConvertRealDuration(TimeSpan.FromSeconds(30)));
    }

    [Fact]
    public void Synchronization_uses_observed_time_and_rejects_clock_rollback()
    {
        var observed = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var clock = WorldClock.Create(
            Guid.NewGuid(),
            observed,
            observed,
            realTimeMultiplier: 4m);

        var elapsed = clock.Synchronize(observed.AddSeconds(30));

        Assert.Equal(TimeSpan.FromMinutes(2), elapsed);
        Assert.Equal(observed.AddMinutes(2), clock.CurrentInstant);
        Assert.Throws<ArgumentOutOfRangeException>(() => clock.Synchronize(observed));
    }

    [Fact]
    public void Rebase_discards_paused_real_time_without_advancing_world_time()
    {
        var observed = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var clock = WorldClock.Create(Guid.NewGuid(), observed, observed, realTimeMultiplier: 3m);

        clock.Rebase(observed.AddHours(8));
        var elapsed = clock.Synchronize(observed.AddHours(8).AddMinutes(2));

        Assert.Equal(TimeSpan.FromMinutes(6), elapsed);
        Assert.Equal(observed.AddMinutes(6), clock.CurrentInstant);
    }
}

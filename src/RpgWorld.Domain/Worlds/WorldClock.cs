namespace RpgWorld.Domain.Worlds;

public sealed class WorldClock
{
    public static readonly TimeSpan DefaultTickDuration = TimeSpan.FromMinutes(1);

    private WorldClock() { }

    private WorldClock(
        Guid worldId,
        DateTimeOffset currentInstant,
        TimeSpan tickDuration,
        decimal realTimeMultiplier,
        DateTimeOffset lastSynchronizedAtUtc)
    {
        WorldId = RequiredId(worldId);
        CurrentInstant = currentInstant.ToUniversalTime();
        SetTickDuration(tickDuration);
        SetRealTimeMultiplier(realTimeMultiplier);
        LastSynchronizedAtUtc = lastSynchronizedAtUtc.ToUniversalTime();
    }

    public Guid WorldId { get; private set; }

    public DateTimeOffset CurrentInstant { get; private set; }

    public TimeSpan TickDuration { get; private set; }

    public decimal RealTimeMultiplier { get; private set; }

    public DateTimeOffset LastSynchronizedAtUtc { get; private set; }

    public static WorldClock Create(
        Guid worldId,
        DateTimeOffset initialInstant,
        DateTimeOffset observedAtUtc,
        TimeSpan? tickDuration = null,
        decimal realTimeMultiplier = 1m) =>
        new(
            worldId,
            initialInstant,
            tickDuration ?? DefaultTickDuration,
            realTimeMultiplier,
            observedAtUtc);

    public TimeSpan AdvanceTicks(int tickCount = 1)
    {
        if (tickCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(tickCount), "Tick count must be positive.");
        }

        var elapsed = TickDuration * tickCount;
        CurrentInstant = CurrentInstant.Add(elapsed);
        return elapsed;
    }

    public void AdvanceBy(TimeSpan duration)
    {
        if (duration <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(duration), "Duration must be positive.");
        }

        CurrentInstant = CurrentInstant.Add(duration);
    }

    public TimeSpan Synchronize(DateTimeOffset observedAtUtc)
    {
        var observedUtc = observedAtUtc.ToUniversalTime();
        var realElapsed = observedUtc - LastSynchronizedAtUtc;

        if (realElapsed < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(observedAtUtc),
                "Real time cannot move backwards.");
        }

        var worldElapsed = ConvertRealDuration(realElapsed);
        CurrentInstant = CurrentInstant.Add(worldElapsed);
        LastSynchronizedAtUtc = observedUtc;
        return worldElapsed;
    }

    public void Rebase(DateTimeOffset observedAtUtc)
    {
        var observedUtc = observedAtUtc.ToUniversalTime();
        if (observedUtc < LastSynchronizedAtUtc)
        {
            throw new ArgumentOutOfRangeException(
                nameof(observedAtUtc),
                "Real time cannot move backwards.");
        }

        LastSynchronizedAtUtc = observedUtc;
    }

    public TimeSpan ConvertRealDuration(TimeSpan realDuration)
    {
        if (realDuration < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(realDuration));
        }

        var scaledTicks = decimal.Round(
            realDuration.Ticks * RealTimeMultiplier,
            0,
            MidpointRounding.ToEven);

        if (scaledTicks > long.MaxValue)
        {
            throw new OverflowException("Scaled world duration is too large.");
        }

        return TimeSpan.FromTicks((long)scaledTicks);
    }

    public void SetTickDuration(TimeSpan tickDuration)
    {
        if (tickDuration <= TimeSpan.Zero || tickDuration > TimeSpan.FromDays(365))
        {
            throw new ArgumentOutOfRangeException(
                nameof(tickDuration),
                "Tick duration must be between one tick and 365 days.");
        }

        TickDuration = tickDuration;
    }

    public void SetRealTimeMultiplier(decimal multiplier)
    {
        if (multiplier is < 0 or > 10_000)
        {
            throw new ArgumentOutOfRangeException(
                nameof(multiplier),
                "Real-time multiplier must be between 0 and 10000.");
        }

        RealTimeMultiplier = multiplier;
    }

    private static Guid RequiredId(Guid value) =>
        value == Guid.Empty
            ? throw new ArgumentException("World identifier cannot be empty.", nameof(value))
            : value;
}

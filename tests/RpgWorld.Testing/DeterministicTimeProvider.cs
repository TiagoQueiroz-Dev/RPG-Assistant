namespace RpgWorld.Testing;

public sealed class DeterministicTimeProvider : TimeProvider
{
    private readonly Lock _lock = new();
    private DateTimeOffset _utcNow;
    private long _timestamp;

    public DeterministicTimeProvider(DateTimeOffset utcNow)
    {
        _utcNow = utcNow.ToUniversalTime();
    }

    public override long TimestampFrequency => TimeSpan.TicksPerSecond;

    public override DateTimeOffset GetUtcNow()
    {
        lock (_lock) return _utcNow;
    }

    public override long GetTimestamp()
    {
        lock (_lock) return _timestamp;
    }

    public void Advance(TimeSpan duration)
    {
        if (duration < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(duration));
        lock (_lock)
        {
            _utcNow = _utcNow.Add(duration);
            _timestamp = checked(_timestamp + duration.Ticks);
        }
    }
}

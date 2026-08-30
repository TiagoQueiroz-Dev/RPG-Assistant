namespace RpgWorld.Simulation.Time;

public sealed record WorldClockSnapshot(
    Guid WorldId,
    DateTimeOffset CurrentInstant,
    TimeSpan TickDuration,
    decimal RealTimeMultiplier,
    DateTimeOffset LastSynchronizedAtUtc);

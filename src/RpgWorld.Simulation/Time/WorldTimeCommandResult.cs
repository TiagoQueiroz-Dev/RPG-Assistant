namespace RpgWorld.Simulation.Time;

public sealed record WorldTimeCommandResult(
    Guid WorldId,
    bool IsRunning,
    DateTimeOffset CurrentInstant,
    TimeSpan TickDuration,
    decimal RealTimeMultiplier,
    string Command);

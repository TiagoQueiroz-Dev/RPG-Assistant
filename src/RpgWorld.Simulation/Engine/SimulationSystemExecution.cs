namespace RpgWorld.Simulation.Engine;

public sealed record SimulationSystemExecution(
    Guid WorldId,
    string SystemName,
    DateTimeOffset ScheduledForUtc,
    DateTimeOffset StartedAtUtc,
    TimeSpan Frequency);

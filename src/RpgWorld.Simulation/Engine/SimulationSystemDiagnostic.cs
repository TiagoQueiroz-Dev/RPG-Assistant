namespace RpgWorld.Simulation.Engine;

public sealed record SimulationSystemDiagnostic(
    Guid WorldId,
    string SystemName,
    TimeSpan Frequency,
    DateTimeOffset? LastStartedAtUtc,
    DateTimeOffset? LastCompletedAtUtc,
    TimeSpan? LastDuration,
    DateTimeOffset NextExecutionAtUtc,
    long ExecutionCount,
    long FailureCount);

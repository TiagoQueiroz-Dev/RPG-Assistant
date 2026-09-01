namespace RpgWorld.Simulation.Engine;

public interface ISimulationPerformanceMetrics
{
    void RecordCycle(int worldCount, TimeSpan duration);
    void RecordTick(Guid worldId, TimeSpan duration, SimulationTickWorkload workload, TimeSpan budget);
    void RecordSystem(Guid worldId, string systemName, TimeSpan duration, long actorsProcessed,
        TimeSpan budget, bool succeeded);
    SimulationPerformanceSnapshot GetSnapshot(Guid? worldId = null);
}

public sealed record SimulationPerformanceSnapshot(
    Guid? WorldId,
    long CycleCount,
    int LastWorldCount,
    double LastCycleDurationMilliseconds,
    long TickCount,
    long LateTickCount,
    double AverageTickDurationMilliseconds,
    double MaximumTickDurationMilliseconds,
    long LastActorsProcessed,
    long TotalActorsProcessed,
    int LastActiveChunkCount,
    IReadOnlyList<SimulationSystemPerformanceSnapshot> Systems);

public sealed record SimulationSystemPerformanceSnapshot(
    string SystemName,
    long ExecutionCount,
    long FailureCount,
    long BudgetExceededCount,
    double AverageDurationMilliseconds,
    double MaximumDurationMilliseconds,
    long LastActorsProcessed,
    long TotalActorsProcessed);

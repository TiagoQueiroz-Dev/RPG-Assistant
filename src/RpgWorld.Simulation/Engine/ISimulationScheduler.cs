namespace RpgWorld.Simulation.Engine;

public interface ISimulationScheduler
{
    bool TryBegin(
        Guid worldId,
        ISimulationSystem system,
        DateTimeOffset observedAtUtc,
        out SimulationSystemExecution? execution);

    void Complete(
        SimulationSystemExecution execution,
        DateTimeOffset completedAtUtc,
        TimeSpan duration,
        bool succeeded);

    IReadOnlyList<SimulationSystemDiagnostic> GetDiagnostics(Guid? worldId = null);
}

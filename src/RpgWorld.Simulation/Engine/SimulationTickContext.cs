using RpgWorld.Simulation.Time;

namespace RpgWorld.Simulation.Engine;

public sealed record SimulationTickContext(
    Guid WorldId,
    WorldClockSnapshot Clock,
    SimulationTickWorkload? Workload = null)
{
    public void RecordActorsProcessed(int count) => Workload?.RecordActors(count);
}

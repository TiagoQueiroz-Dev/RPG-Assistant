namespace RpgWorld.Simulation.Engine;

public interface ISimulationSystemRunner
{
    Task RunAsync(SimulationTickContext context, CancellationToken cancellationToken = default);
}

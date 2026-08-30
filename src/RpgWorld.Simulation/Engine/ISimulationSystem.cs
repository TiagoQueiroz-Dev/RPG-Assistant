namespace RpgWorld.Simulation.Engine;

public interface ISimulationSystem
{
    string Name { get; }

    int Order { get; }

    Task ExecuteAsync(
        SimulationTickContext context,
        CancellationToken cancellationToken = default);
}

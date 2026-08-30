namespace RpgWorld.Simulation.Engine;

public interface ISimulationSystem
{
    string Name { get; }

    int Order { get; }

    TimeSpan Frequency { get; }

    Task ExecuteAsync(
        SimulationTickContext context,
        CancellationToken cancellationToken = default);
}

namespace RpgWorld.Simulation.Engine;

public interface IWorldSimulationControlService
{
    Task<WorldSimulationStatus> GetStatusAsync(
        Guid worldId,
        CancellationToken cancellationToken = default);

    Task<WorldSimulationStatus> StartAsync(
        Guid worldId,
        CancellationToken cancellationToken = default);

    Task<WorldSimulationStatus> PauseAsync(
        Guid worldId,
        CancellationToken cancellationToken = default);
}

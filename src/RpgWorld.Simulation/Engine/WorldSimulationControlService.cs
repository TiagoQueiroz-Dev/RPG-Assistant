using RpgWorld.Application.Worlds;

namespace RpgWorld.Simulation.Engine;

public sealed class WorldSimulationControlService(IWorldSimulationRepository repository)
    : IWorldSimulationControlService
{
    public async Task<WorldSimulationStatus> GetStatusAsync(
        Guid worldId,
        CancellationToken cancellationToken = default) =>
        Snapshot(await GetWorldAsync(worldId, cancellationToken));

    public async Task<WorldSimulationStatus> StartAsync(
        Guid worldId,
        CancellationToken cancellationToken = default)
    {
        var world = await GetWorldAsync(worldId, cancellationToken);
        world.StartSimulation();
        await repository.SaveChangesAsync(cancellationToken);
        return Snapshot(world);
    }

    public async Task<WorldSimulationStatus> PauseAsync(
        Guid worldId,
        CancellationToken cancellationToken = default)
    {
        var world = await GetWorldAsync(worldId, cancellationToken);
        world.PauseSimulation();
        await repository.SaveChangesAsync(cancellationToken);
        return Snapshot(world);
    }

    private async Task<RpgWorld.Domain.Worlds.World> GetWorldAsync(
        Guid worldId,
        CancellationToken cancellationToken) =>
        await repository.GetAsync(worldId, cancellationToken)
        ?? throw new KeyNotFoundException("World was not found.");

    private static WorldSimulationStatus Snapshot(
        RpgWorld.Domain.Worlds.World world) =>
        new(world.Id, world.IsSimulationRunning);
}

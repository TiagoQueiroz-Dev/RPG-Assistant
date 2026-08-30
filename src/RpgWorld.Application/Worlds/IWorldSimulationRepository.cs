using RpgWorld.Domain.Worlds;

namespace RpgWorld.Application.Worlds;

public interface IWorldSimulationRepository
{
    Task<IReadOnlyList<Guid>> ListRunningWorldIdsAsync(
        CancellationToken cancellationToken = default);

    Task<World?> GetAsync(
        Guid worldId,
        CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

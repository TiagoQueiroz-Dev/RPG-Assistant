using RpgWorld.Domain.Worlds;

namespace RpgWorld.Simulation.Regions;

public interface IRegionSimulationService
{
    Task<IReadOnlyList<RegionSimulationTransition>> SynchronizeAsync(
        World world,
        IEnumerable<Position> playerPositions,
        IEnumerable<ChunkCoordinate>? activeRegions = null,
        CancellationToken cancellationToken = default);
}

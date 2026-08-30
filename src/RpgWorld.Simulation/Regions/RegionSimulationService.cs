using RpgWorld.Application.Worlds;
using RpgWorld.Domain.Worlds;

namespace RpgWorld.Simulation.Regions;

public sealed class RegionSimulationService(
    IRegionSimulationRepository repository,
    SimulationLevelResolver resolver) : IRegionSimulationService
{
    public async Task<IReadOnlyList<RegionSimulationTransition>> SynchronizeAsync(
        World world,
        IEnumerable<Position> playerPositions,
        IEnumerable<ChunkCoordinate>? activeRegions = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(world);
        var players = playerPositions?.ToArray()
            ?? throw new ArgumentNullException(nameof(playerPositions));
        var active = (activeRegions ?? []).ToHashSet();
        var chunks = await repository.ListChunksAsync(world.Id, cancellationToken);
        var transitions = new List<RegionSimulationTransition>();

        foreach (var chunk in chunks)
        {
            var target = resolver.Resolve(world, chunk.Coordinate, players, active.Contains(chunk.Coordinate));
            if (target == chunk.SimulationLevel) continue;
            var previous = chunk.SimulationLevel;
            var tiles = await repository.ListTilesAsync(world.Id, chunk.Coordinate, cancellationToken);
            var actorIds = tiles.SelectMany(tile => tile.OccupantIds).Distinct().Order().ToArray();
            var aggregate = new RegionAggregateState(
                actorIds.Length,
                tiles.Count(tile => tile.StructureId is not null),
                chunk.AggregateMilitaryStrength,
                tiles.Count(tile => tile.ResourceDepositId is not null));
            chunk.TransitionSimulationLevel(target, aggregate);
            transitions.Add(new RegionSimulationTransition(
                chunk.Id,
                chunk.Coordinate,
                previous,
                target,
                aggregate,
                target == SimulationLevel.Detailed ? actorIds : []));
        }

        if (transitions.Count > 0) await repository.SaveChangesAsync(cancellationToken);
        return transitions;
    }
}

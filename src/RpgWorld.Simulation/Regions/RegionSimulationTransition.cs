using RpgWorld.Domain.Worlds;

namespace RpgWorld.Simulation.Regions;

public sealed record RegionSimulationTransition(
    Guid ChunkId,
    ChunkCoordinate Coordinate,
    SimulationLevel PreviousLevel,
    SimulationLevel CurrentLevel,
    RegionAggregateState AggregateState,
    IReadOnlyList<Guid> MaterializedActorIds);

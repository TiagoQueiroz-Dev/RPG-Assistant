using RpgWorld.Application.Worlds;
using RpgWorld.Domain.Worlds;
using RpgWorld.Domain.Worlds.Definitions;
using RpgWorld.Simulation.Regions;

namespace RpgWorld.Simulation.Tests.Regions;

public sealed class RegionSimulationServiceTests
{
    private static readonly WorldDefinitionCatalog Definitions = new(
        [new TerrainDefinition("plains", "Plains", 1m, true, false)],
        [new BiomeDefinition("grassland", "Grassland", "plains", -20m, 45m, 0m, 1m)]);

    [Fact]
    public async Task Distance_transitions_aggregate_and_materialize_without_losing_actors()
    {
        var world = World.Create("Wide world", 160, 32);
        var repository = FakeRepository.Create(world);
        var resolver = new SimulationLevelResolver(new SimulationLevelOptions(0, 2));
        var service = new RegionSimulationService(repository, resolver);

        var approached = await service.SynchronizeAsync(world, [world.PositionAt(1, 1)]);

        Assert.Equal(3, approached.Count);
        Assert.Equal(SimulationLevel.Detailed, repository.ChunkAt(0).SimulationLevel);
        Assert.Equal(SimulationLevel.Regional, repository.ChunkAt(1).SimulationLevel);
        Assert.Equal(SimulationLevel.Regional, repository.ChunkAt(2).SimulationLevel);
        Assert.Equal(SimulationLevel.Abstract, repository.ChunkAt(3).SimulationLevel);
        Assert.True(repository.ChunkAt(0).AllowsIndividualActions);
        Assert.False(repository.ChunkAt(1).AllowsIndividualActions);

        var movedAway = await service.SynchronizeAsync(world, [world.PositionAt(159, 1)]);

        var aggregated = Assert.Single(movedAway, transition => transition.Coordinate.X == 0);
        Assert.Equal(SimulationLevel.Abstract, aggregated.CurrentLevel);
        Assert.Equal(1, aggregated.AggregateState.Population);
        Assert.Equal(1m, aggregated.AggregateState.EconomicOutput);
        Assert.Equal(1m, aggregated.AggregateState.ProductionOutput);
        Assert.False(repository.ChunkAt(0).AllowsIndividualActions);
        var materialized = Assert.Single(movedAway, transition => transition.Coordinate.X == 4);
        Assert.Equal(SimulationLevel.Detailed, materialized.CurrentLevel);
        Assert.Equal([repository.ActorAt(4)], materialized.MaterializedActorIds);
        Assert.True(repository.ChunkAt(4).AllowsIndividualActions);
        Assert.Equal(2, repository.SaveCalls);
    }

    [Fact]
    public async Task Relevant_activity_promotes_distant_region_to_detailed()
    {
        var world = World.Create("Active world", 96, 32);
        var repository = FakeRepository.Create(world);
        var service = new RegionSimulationService(
            repository,
            new SimulationLevelResolver(new SimulationLevelOptions(0, 1)));

        var transitions = await service.SynchronizeAsync(
            world,
            [],
            [new ChunkCoordinate(2, 0)]);

        var transition = Assert.Single(transitions);
        Assert.Equal(new ChunkCoordinate(2, 0), transition.Coordinate);
        Assert.Equal(SimulationLevel.Detailed, transition.CurrentLevel);
    }

    private sealed class FakeRepository : IRegionSimulationRepository
    {
        private readonly Dictionary<ChunkCoordinate, Chunk> _chunks = [];
        private readonly Dictionary<ChunkCoordinate, IReadOnlyList<Tile>> _tiles = [];
        private readonly Dictionary<int, Guid> _actors = [];

        public int SaveCalls { get; private set; }

        public Chunk ChunkAt(int x) => _chunks[new ChunkCoordinate(x, 0)];

        public Guid ActorAt(int x) => _actors[x];

        public static FakeRepository Create(World world)
        {
            var repository = new FakeRepository();
            for (var x = 0; x < world.ChunkColumns; x++)
            {
                var coordinate = new ChunkCoordinate(x, 0);
                var chunk = world.CreateChunk(coordinate);
                var tile = world.CreateTile(
                    world.PositionAt(chunk.OriginX, 0),
                    "grassland",
                    Definitions,
                    0,
                    20m,
                    0.5m);
                var actorId = Guid.NewGuid();
                tile.AddOccupant(actorId);
                if (x == 0)
                {
                    tile.AssignStructure(Guid.NewGuid());
                    tile.AssignResource(Guid.NewGuid());
                }
                repository._chunks.Add(coordinate, chunk);
                repository._tiles.Add(coordinate, [tile]);
                repository._actors.Add(x, actorId);
            }
            return repository;
        }

        public Task<IReadOnlyList<Chunk>> ListChunksAsync(
            Guid worldId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Chunk>>(_chunks.Values.OrderBy(chunk => chunk.CoordinateX).ToArray());

        public Task<IReadOnlyList<Tile>> ListTilesAsync(
            Guid worldId,
            ChunkCoordinate coordinate,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(_tiles[coordinate]);

        public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            SaveCalls++;
            return Task.CompletedTask;
        }
    }
}

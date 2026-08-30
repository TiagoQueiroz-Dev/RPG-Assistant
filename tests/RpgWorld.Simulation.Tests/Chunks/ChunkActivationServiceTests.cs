using RpgWorld.Application.Caching;
using RpgWorld.Application.Events;
using RpgWorld.Application.Worlds;
using RpgWorld.Domain.Events;
using RpgWorld.Domain.Worlds;
using RpgWorld.Domain.Worlds.Definitions;
using RpgWorld.Simulation.Chunks;
using RpgWorld.Simulation.Regions;

namespace RpgWorld.Simulation.Tests.Chunks;

public sealed class ChunkActivationServiceTests
{
    private static readonly WorldDefinitionCatalog Definitions = new(
        [new TerrainDefinition("plains", "Plains", 1m, true, false)],
        [new BiomeDefinition("grassland", "Grassland", "plains", -20m, 45m, 0m, 1m)]);

    [Fact]
    public async Task Player_activates_neighboring_chunks_and_relevant_region()
    {
        var world = World.Create("Aster", 128, 128);
        var repository = InMemoryWorldMapRepository.Create(world);
        var cache = new RecordingCacheService();
        var dispatcher = new RecordingEventDispatcher();
        var clock = new ManualTimeProvider(new DateTimeOffset(2026, 8, 30, 12, 0, 0, TimeSpan.Zero));
        var regionSimulation = new RecordingRegionSimulationService();
        using var service = new ChunkActivationService(
            repository,
            cache,
            dispatcher,
            new ChunkActivationOptions(playerRadius: 1),
            clock,
            regionSimulation);

        await service.SynchronizeAsync(
            world,
            [world.PositionAt(40, 40)],
            [new ChunkCoordinate(3, 3)]);

        var active = service.GetActiveChunks(world.Id);
        Assert.Equal(10, active.Count);
        Assert.True(service.TryGetActiveChunk(world.Id, new ChunkCoordinate(3, 3), out _));
        Assert.Equal(10, cache.SetKeys.Count);
        Assert.Equal(
            10,
            dispatcher.Events.OfType<ChunkActivatedEvent>().Count());
        Assert.Equal(1, regionSimulation.SyncCalls);
        Assert.Equal(world.PositionAt(40, 40), Assert.Single(regionSimulation.PlayerPositions));
    }

    [Fact]
    public async Task Distant_chunk_is_persisted_evicted_and_restored_after_timeout()
    {
        var world = World.Create("Aster", 64, 32);
        var repository = InMemoryWorldMapRepository.Create(world);
        var cache = new RecordingCacheService();
        var dispatcher = new RecordingEventDispatcher();
        var clock = new ManualTimeProvider(new DateTimeOffset(2026, 8, 30, 12, 0, 0, TimeSpan.Zero));
        using var service = new ChunkActivationService(
            repository,
            cache,
            dispatcher,
            new ChunkActivationOptions(playerRadius: 0, inactivityTimeout: TimeSpan.FromMinutes(5)),
            clock);

        await service.SynchronizeAsync(world, [world.PositionAt(1, 1)]);
        Assert.True(service.TryGetActiveChunk(
            world.Id,
            new ChunkCoordinate(0, 0),
            out var firstChunk));
        var structureId = Guid.NewGuid();
        Assert.NotNull(firstChunk);
        Assert.NotEmpty(firstChunk.Tiles);
        firstChunk.Tiles[0].AssignStructure(structureId);

        clock.Advance(TimeSpan.FromMinutes(1));
        await service.SynchronizeAsync(world, [world.PositionAt(40, 1)]);
        Assert.Equal(2, service.GetActiveChunks(world.Id).Count);

        clock.Advance(TimeSpan.FromMinutes(5));
        await service.SynchronizeAsync(world, [world.PositionAt(40, 1)]);

        Assert.False(service.TryGetActiveChunk(world.Id, new ChunkCoordinate(0, 0), out _));
        Assert.Single(service.GetActiveChunks(world.Id));
        Assert.Equal(1, repository.PersistCalls);
        Assert.Contains(
            CacheKeys.ActiveChunk(world.Id, 0, 0),
            cache.RemovedKeys);
        var deactivated = Assert.Single(dispatcher.Events.OfType<ChunkDeactivatedEvent>());
        Assert.Equal(TimeSpan.FromMinutes(6), deactivated.InactiveFor);

        await service.SynchronizeAsync(world, [world.PositionAt(1, 1)]);

        Assert.True(service.TryGetActiveChunk(
            world.Id,
            new ChunkCoordinate(0, 0),
            out var reloadedChunk));
        Assert.Equal(structureId, reloadedChunk!.Tiles[0].StructureId);
    }

    private sealed class InMemoryWorldMapRepository : IWorldMapRepository
    {
        private readonly World _world;
        private readonly Dictionary<ChunkCoordinate, Chunk> _chunks;
        private readonly Dictionary<ChunkCoordinate, IReadOnlyList<Tile>> _tiles;

        private InMemoryWorldMapRepository(
            World world,
            Dictionary<ChunkCoordinate, Chunk> chunks,
            Dictionary<ChunkCoordinate, IReadOnlyList<Tile>> tiles)
        {
            _world = world;
            _chunks = chunks;
            _tiles = tiles;
        }

        public int PersistCalls { get; private set; }

        public static InMemoryWorldMapRepository Create(World world)
        {
            var chunks = new Dictionary<ChunkCoordinate, Chunk>();
            var tiles = new Dictionary<ChunkCoordinate, IReadOnlyList<Tile>>();

            for (var y = 0; y < world.ChunkRows; y++)
            {
                for (var x = 0; x < world.ChunkColumns; x++)
                {
                    var coordinate = new ChunkCoordinate(x, y);
                    var chunk = world.CreateChunk(coordinate);
                    var tile = world.CreateTile(
                        world.PositionAt(chunk.OriginX, chunk.OriginY),
                        "grassland",
                        Definitions,
                        elevation: 0,
                        temperatureCelsius: 20m,
                        humidity: 0.50m);
                    chunks.Add(coordinate, chunk);
                    tiles.Add(coordinate, [tile]);
                }
            }

            return new InMemoryWorldMapRepository(world, chunks, tiles);
        }

        public Task<World?> GetWorldAsync(
            Guid worldId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<World?>(worldId == _world.Id ? _world : null);

        public Task<Chunk?> GetChunkAsync(
            Guid worldId,
            ChunkCoordinate coordinate,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<Chunk?>(
                worldId == _world.Id && _chunks.TryGetValue(coordinate, out var chunk)
                    ? chunk
                    : null);

        public Task<Tile?> GetTileAsync(
            Position position,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(
                _tiles.Values.SelectMany(tiles => tiles).SingleOrDefault(tile => tile.Position == position));

        public Task<IReadOnlyList<Tile>> GetTilesAsync(
            Guid worldId,
            ChunkCoordinate coordinate,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(
                worldId == _world.Id && _tiles.TryGetValue(coordinate, out var tiles)
                    ? tiles
                    : (IReadOnlyList<Tile>)[]);

        public Task PersistAndReleaseChunkAsync(
            Chunk chunk,
            IReadOnlyCollection<Tile> tiles,
            CancellationToken cancellationToken = default)
        {
            PersistCalls++;
            _chunks[chunk.Coordinate] = chunk;
            _tiles[chunk.Coordinate] = tiles.ToArray();
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingCacheService : ICacheService
    {
        public List<CacheKey> SetKeys { get; } = [];

        public List<CacheKey> RemovedKeys { get; } = [];

        public Task<CacheReadResult<T>> GetAsync<T>(
            CacheKey key,
            CancellationToken cancellationToken = default)
            where T : notnull =>
            Task.FromResult(CacheReadResult<T>.Miss());

        public Task SetAsync<T>(
            CacheKey key,
            T value,
            CacheEntryOptions options,
            CancellationToken cancellationToken = default)
            where T : notnull
        {
            SetKeys.Add(key);
            return Task.CompletedTask;
        }

        public Task RemoveAsync(
            CacheKey key,
            CancellationToken cancellationToken = default)
        {
            RemovedKeys.Add(key);
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingEventDispatcher : IDomainEventDispatcher
    {
        public List<IDomainEvent> Events { get; } = [];

        public Task DispatchAsync(
            IReadOnlyCollection<IDomainEvent> domainEvents,
            CancellationToken cancellationToken = default)
        {
            Events.AddRange(domainEvents);
            return Task.CompletedTask;
        }
    }

    private sealed class ManualTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;

        public void Advance(TimeSpan duration) => utcNow += duration;
    }

    private sealed class RecordingRegionSimulationService : IRegionSimulationService
    {
        public int SyncCalls { get; private set; }
        public IReadOnlyList<Position> PlayerPositions { get; private set; } = [];

        public Task<IReadOnlyList<RegionSimulationTransition>> SynchronizeAsync(
            World world,
            IEnumerable<Position> playerPositions,
            IEnumerable<ChunkCoordinate>? activeRegions = null,
            CancellationToken cancellationToken = default)
        {
            SyncCalls++;
            PlayerPositions = playerPositions.ToArray();
            return Task.FromResult<IReadOnlyList<RegionSimulationTransition>>([]);
        }
    }
}

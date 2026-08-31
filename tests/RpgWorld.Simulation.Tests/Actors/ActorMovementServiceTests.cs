using RpgWorld.Application.Actors.Movement;
using RpgWorld.Application.Caching;
using RpgWorld.Application.Realtime;
using RpgWorld.Domain.Actors;
using RpgWorld.Domain.Events;
using RpgWorld.Domain.Worlds;
using RpgWorld.Domain.Worlds.Definitions;
using RpgWorld.Simulation.Actors;
using RpgWorld.Simulation.Chunks;
using RpgWorld.Simulation.Engine;

namespace RpgWorld.Simulation.Tests.Actors;

public sealed class ActorMovementServiceTests
{
    private static readonly WorldDefinitionCatalog Definitions = new(
        [
            new TerrainDefinition("road", "Road", 1.25m, true, false),
            new TerrainDefinition("wall", "Wall", 3m, false, false)
        ],
        [
            new BiomeDefinition("settled", "Settled", "road", -20m, 50m, 0m, 1m, 1.2m),
            new BiomeDefinition("blocked", "Blocked", "wall", -20m, 50m, 0m, 1m)
        ]);

    [Theory]
    [InlineData("player")]
    [InlineData("npc")]
    [InlineData("creature")]
    public async Task Every_actor_kind_crosses_chunk_atomically_and_notifies_only_relevant_chunks(string kind)
    {
        var world = World.Create("Movement", 64, 32);
        var actor = CreateActor(kind, world, world.PositionAt(31, 0));
        actor.ClearDomainEvents();
        var origin = CreateTile(world, 31, 0, "settled");
        var destination = CreateTile(world, 32, 0, "settled");
        origin.AddOccupant(actor.Id);
        var store = new FakeMovementStore(world, actor, [origin, destination]);
        var activation = new RecordingChunkActivationService();
        var publisher = new RecordingPublisher();
        var service = CreateService(store, activation, publisher);

        var result = await service.MoveAsync(new ActorMoveRequest(actor.Id, 32, 0));

        Assert.Equal(world.PositionAt(32, 0), actor.Position);
        Assert.DoesNotContain(actor.Id, origin.OccupantIds);
        Assert.Contains(actor.Id, destination.OccupantIds);
        Assert.True(result.CrossedChunkBoundary);
        Assert.Equal(1.5m, result.MovementCost);
        Assert.Equal(1, store.SaveCalls);
        Assert.Equal((actor.Id, result.Origin, result.Destination), activation.LastMovement);
        Assert.Equal([result.OriginChunkId, result.DestinationChunkId], publisher.ChunkIds);
        Assert.All(publisher.Messages, message => Assert.Equal("actor.moved", message.UpdateType));
        var moved = Assert.IsType<ActorMovedEvent>(Assert.Single(actor.DomainEvents));
        Assert.Equal(result.Destination, moved.Destination);
    }

    [Fact]
    public async Task Impassable_terrain_is_rejected_without_mutating_state()
    {
        var world = World.Create("Blocked", 8, 8);
        var actor = PlayerActor.Create("Ayla", world, world.PositionAt(1, 1), DateTimeOffset.UnixEpoch);
        var origin = CreateTile(world, 1, 1, "settled");
        var destination = CreateTile(world, 2, 1, "blocked");
        origin.AddOccupant(actor.Id);
        var store = new FakeMovementStore(world, actor, [origin, destination]);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateService(store).MoveAsync(new ActorMoveRequest(actor.Id, 2, 1)));

        Assert.Contains("blocks movement", exception.Message, StringComparison.Ordinal);
        Assert.Equal(world.PositionAt(1, 1), actor.Position);
        Assert.Equal(0, store.SaveCalls);
        Assert.Contains(actor.Id, origin.OccupantIds);
    }

    [Theory]
    [InlineData(4, 1)]
    [InlineData(-1, 1)]
    [InlineData(8, 1)]
    public async Task Non_adjacent_or_outside_destination_is_rejected(int x, int y)
    {
        var world = World.Create("Limits", 8, 8);
        var actor = CreatureActor.Create("Wolf", world, world.PositionAt(1, 1), DateTimeOffset.UnixEpoch);
        var origin = CreateTile(world, 1, 1, "settled");
        var destination = x is >= 0 and < 8
            ? CreateTile(world, x, y, "settled")
            : null;
        var store = new FakeMovementStore(world, actor, destination is null ? [origin] : [origin, destination]);

        var exception = await Record.ExceptionAsync(() =>
            CreateService(store).MoveAsync(new ActorMoveRequest(actor.Id, x, y)));

        Assert.True(exception is ArgumentException or InvalidOperationException);
        Assert.Equal(world.PositionAt(1, 1), actor.Position);
        Assert.Equal(0, store.SaveCalls);
    }

    private static ActorMovementService CreateService(
        FakeMovementStore store,
        RecordingChunkActivationService? activation = null,
        RecordingPublisher? publisher = null) =>
        new(
            store,
            Definitions,
            new AdjacentTileMovementPolicy(),
            activation ?? new RecordingChunkActivationService(),
            new WorldCommandGate(),
            publisher ?? new RecordingPublisher(),
            new FixedTimeProvider());

    private static Actor CreateActor(string kind, World world, Position position) => kind switch
    {
        "player" => PlayerActor.Create("Player", world, position, DateTimeOffset.UnixEpoch),
        "npc" => NpcActor.Create("NPC", world, position, DateTimeOffset.UnixEpoch),
        "creature" => CreatureActor.Create("Creature", world, position, DateTimeOffset.UnixEpoch),
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };

    private static Tile CreateTile(World world, int x, int y, string biome) =>
        world.CreateTile(world.PositionAt(x, y), biome, Definitions, 0, 20m, 0.5m);

    private sealed class FakeMovementStore : IActorMovementStore
    {
        private readonly World _world;
        private readonly Actor _actor;
        private readonly Dictionary<Position, Tile> _tiles;
        private readonly Dictionary<ChunkCoordinate, Chunk> _chunks;

        public FakeMovementStore(World world, Actor actor, IEnumerable<Tile> tiles)
        {
            _world = world;
            _actor = actor;
            _tiles = tiles.ToDictionary(tile => tile.Position);
            _chunks = Enumerable.Range(0, world.ChunkColumns)
                .Select(x => world.CreateChunk(new ChunkCoordinate(x, 0)))
                .ToDictionary(chunk => chunk.Coordinate);
        }

        public int SaveCalls { get; private set; }
        public Task<Guid?> FindActorWorldIdAsync(Guid actorId, CancellationToken cancellationToken = default) =>
            Task.FromResult<Guid?>(actorId == _actor.Id ? _world.Id : null);
        public Task<Actor?> GetActorAsync(Guid actorId, CancellationToken cancellationToken = default) =>
            Task.FromResult<Actor?>(actorId == _actor.Id ? _actor : null);
        public Task<World?> GetWorldAsync(Guid worldId, CancellationToken cancellationToken = default) =>
            Task.FromResult<World?>(worldId == _world.Id ? _world : null);
        public Task<Tile?> GetTileAsync(Position position, CancellationToken cancellationToken = default) =>
            Task.FromResult(_tiles.GetValueOrDefault(position));
        public Task<Chunk?> GetChunkAsync(Guid worldId, ChunkCoordinate coordinate, CancellationToken cancellationToken = default) =>
            Task.FromResult<Chunk?>(worldId == _world.Id ? _chunks.GetValueOrDefault(coordinate) : null);
        public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        { SaveCalls++; return Task.CompletedTask; }
    }

    private sealed class RecordingChunkActivationService : IChunkActivationService
    {
        public (Guid ActorId, Position Origin, Position Destination)? LastMovement { get; private set; }
        public IReadOnlyCollection<ActiveChunk> GetActiveChunks(Guid worldId) => [];
        public bool TryGetActiveChunk(Guid worldId, ChunkCoordinate coordinate, out ActiveChunk? activeChunk)
        { activeChunk = null; return false; }
        public Task SynchronizeAsync(World world, IEnumerable<Position> playerPositions, IEnumerable<ChunkCoordinate>? relevantRegions = null, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task ApplyActorMovementAsync(Guid worldId, Guid actorId, Position origin, Position destination, CancellationToken cancellationToken = default)
        {
            LastMovement = (actorId, origin, destination);
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingPublisher : IWorldUpdatePublisher
    {
        public List<Guid> ChunkIds { get; } = [];
        public List<WorldUpdateMessage> Messages { get; } = [];
        public Task PublishToChunkAsync(Guid chunkId, WorldUpdateMessage message, CancellationToken cancellationToken = default)
        { ChunkIds.Add(chunkId); Messages.Add(message); return Task.CompletedTask; }
        public Task PublishToWorldAsync(WorldUpdateMessage message, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task PublishToPlayerAsync(Guid playerId, WorldUpdateMessage message, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task PublishToGameMasterAsync(WorldUpdateMessage message, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(2026, 8, 31, 0, 0, 0, TimeSpan.Zero);
    }
}

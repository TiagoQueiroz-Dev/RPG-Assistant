using RpgWorld.Application.Actors.Movement;
using RpgWorld.Domain.Actors;
using RpgWorld.Domain.Worlds;
using RpgWorld.Domain.Worlds.Definitions;
using RpgWorld.Simulation.Actors;

namespace RpgWorld.Simulation.Tests.Actors;

public sealed class AStarActorPathfinderTests
{
    private static readonly WorldDefinitionCatalog Definitions = new(
        [new("plain", "Plain", 1m, true, false), new("rock", "Rock", 10m, true, false), new("water", "Water", 1m, false, true)],
        [new("plain", "Plain", "plain", 0m, 40m, 0m, 1m), new("rock", "Rock", "rock", 0m, 40m, 0m, 1m),
         new("water", "Water", "water", 0m, 40m, 0m, 1m)]);

    [Fact]
    public async Task Finds_deterministic_adjacent_steps_and_zero_length_route_at_destination()
    {
        var (map, actor, finder) = Create();
        var destination = map.World.PositionAt(6, 2);
        var first = await finder.FindAsync(actor, destination);
        var second = await finder.FindAsync(actor, destination);
        Assert.Equal(ActorPathStatus.Found, first.Status);
        Assert.Equal(first.Steps, second.Steps);
        Assert.Equal(destination, first.Steps[^1]);
        Assert.Equal(6m, first.TotalCost);
        ValidateSteps(actor, first, map);
        var same = await finder.FindAsync(actor, actor.Position);
        Assert.Equal(ActorPathStatus.Found, same.Status);
        Assert.Empty(same.Steps);
    }

    [Fact]
    public async Task Terrain_costs_prefer_longer_but_cheaper_route()
    {
        var (map, actor, finder) = Create();
        foreach (var tile in map.Tiles.Where(tile => tile.Y == 2 && tile.X is > 0 and < 6))
            tile.SetEnvironment("rock", Definitions, 0, 20m, 0.5m);
        var path = await finder.FindAsync(actor, map.World.PositionAt(6, 2));
        Assert.Equal(ActorPathStatus.Found, path.Status);
        Assert.Equal(6.8284m, path.TotalCost);
        Assert.DoesNotContain(path.Steps, step => step.Y == 2 && step.X is > 0 and < 6);
        ValidateSteps(actor, path, map);
    }

    [Fact]
    public async Task Obstacles_and_world_changes_recompute_a_valid_route()
    {
        var (map, actor, finder) = Create();
        foreach (var tile in map.Tiles.Where(tile => tile.X == 3 && tile.Y != 0))
            tile.SetEnvironment("water", Definitions, 0, 20m, 0.5m);
        var first = await finder.FindAsync(actor, map.World.PositionAt(6, 2));
        Assert.Equal(ActorPathStatus.Found, first.Status);
        Assert.Contains(map.World.PositionAt(3, 0), first.Steps);
        ValidateSteps(actor, first, map);
        map.At(3, 0).SetEnvironment("water", Definitions, 0, 20m, 0.5m);
        var blocked = await finder.FindAsync(actor, map.World.PositionAt(6, 2));
        Assert.Equal(ActorPathStatus.NoPath, blocked.Status);
        Assert.Empty(blocked.Steps);
        map.At(3, 4).SetEnvironment("plain", Definitions, 0, 20m, 0.5m);
        var revised = await finder.FindAsync(actor, map.World.PositionAt(6, 2));
        Assert.Equal(ActorPathStatus.Found, revised.Status);
        Assert.Contains(map.World.PositionAt(3, 4), revised.Steps);
        ValidateSteps(actor, revised, map);
    }

    [Fact]
    public async Task Budgets_bound_work_and_do_not_report_unreachable_when_search_was_truncated()
    {
        var (map, actor, finder) = Create();
        var destination = map.World.PositionAt(6, 2);
        var nodes = await finder.FindAsync(actor, destination, new(MaximumExpandedNodes: 1));
        Assert.Equal((ActorPathStatus.SearchLimitReached, 1), (nodes.Status, nodes.ExpandedNodes));
        var loads = map.Loads;
        var tiles = await finder.FindAsync(actor, destination, new(MaximumLoadedTiles: 1));
        Assert.Equal(ActorPathStatus.SearchLimitReached, tiles.Status);
        Assert.Equal(loads, map.Loads);
        map.At(3, 2).SetEnvironment("water", Definitions, 0, 20m, 0.5m);
        var narrow = await finder.FindAsync(actor, destination, new(SearchPadding: 0));
        Assert.Equal(ActorPathStatus.SearchLimitReached, narrow.Status);
        Assert.Equal(ActorPathStatus.Found, (await finder.FindAsync(actor, destination)).Status);
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => finder.FindAsync(actor, destination, new(MaximumExpandedNodes: 0)));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => finder.FindAsync(actor, destination,
            cancellationToken: new CancellationToken(true)));
    }

    [Fact]
    public async Task Invalid_world_and_blocked_destination_are_explicit()
    {
        var (map, actor, finder) = Create();
        await Assert.ThrowsAsync<ArgumentException>(() => finder.FindAsync(actor, new Position(Guid.NewGuid(), 1, 1)));
        map.At(6, 2).SetEnvironment("water", Definitions, 0, 20m, 0.5m);
        Assert.Equal(ActorPathStatus.NoPath, (await finder.FindAsync(actor, map.World.PositionAt(6, 2))).Status);
    }

    private static (MapStore, NpcActor, AStarActorPathfinder) Create()
    {
        var world = World.Create("Navigation", 7, 5, 4);
        var tiles = (from y in Enumerable.Range(0, 5) from x in Enumerable.Range(0, 7)
            select world.CreateTile(world.PositionAt(x, y), "plain", Definitions, 0, 20m, 0.5m)).ToArray();
        var map = new MapStore(world, tiles);
        var actor = NpcActor.Create("Walker", world, world.PositionAt(0, 2), DateTimeOffset.UnixEpoch);
        return (map, actor, new(map, Definitions, new AdjacentTileMovementPolicy()));
    }

    private static void ValidateSteps(Actor actor, ActorPathResult path, MapStore map)
    {
        var origin = actor.Position;
        var cost = 0m;
        foreach (var step in path.Steps)
        {
            cost += new AdjacentTileMovementPolicy().Evaluate(actor, map.At(origin.X, origin.Y), map.At(step.X, step.Y), Definitions).MovementCost;
            origin = step;
        }
        Assert.Equal(path.TotalCost, cost);
    }

    private sealed class MapStore(World world, Tile[] tiles) : IPathfindingMapStore
    {
        public World World => world;
        public Tile[] Tiles => tiles;
        public int Loads { get; private set; }
        public Tile At(int x, int y) => tiles.Single(tile => tile.X == x && tile.Y == y);
        public Task<World?> GetWorldAsync(Guid worldId, CancellationToken cancellationToken = default) => Task.FromResult<World?>(world);
        public Task<IReadOnlyList<Tile>> GetTilesAsync(Guid worldId, NavigationBounds bounds, int limit, CancellationToken cancellationToken = default)
        {
            Loads++;
            return Task.FromResult<IReadOnlyList<Tile>>(tiles.Where(tile => tile.X >= bounds.MinX && tile.X <= bounds.MaxX &&
                tile.Y >= bounds.MinY && tile.Y <= bounds.MaxY).Take(limit).ToArray());
        }
    }
}

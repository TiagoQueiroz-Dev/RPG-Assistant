using RpgWorld.Application.Actors.Housing;
using RpgWorld.Domain.Actors;
using RpgWorld.Domain.Actors.Housing;
using RpgWorld.Domain.Worlds;
using RpgWorld.Domain.Worlds.Definitions;
using RpgWorld.Simulation.Actors.Housing;
using RpgWorld.Simulation.Engine;
using RpgWorld.Simulation.Time;

namespace RpgWorld.Simulation.Tests.Actors;

public sealed class NpcHousingSimulationSystemTests
{
    private static readonly WorldDefinitionCatalog Definitions = new(
        [new TerrainDefinition("plains", "Plains", 1m, true, false), new TerrainDefinition("water", "Water", 1m, false, true)],
        [new BiomeDefinition("grassland", "Grassland", "plains", -10m, 40m, 0m, 1m), new BiomeDefinition("ocean", "Ocean", "water", -10m, 40m, 0m, 1m)]);

    [Fact]
    public async Task Homeless_npc_builds_persistent_map_structure_in_two_cycles()
    {
        var now = DateTimeOffset.UnixEpoch;
        var world = World.Create("Autonomous housing", 8, 8);
        var npc = NpcActor.Create("Builder", world, world.PositionAt(1, 1), now);
        var family = NpcActor.Create("Family", world, world.PositionAt(1, 1), now);
        npc.AddFamilyMember(family.Id, now);
        npc.AddInventory("wood", 4, now);
        npc.AddInventory("stone", 2, now);
        var tile = world.CreateTile(world.PositionAt(2, 1), "grassland", Definitions, 0, 20m, 0.5m);
        var repository = new FakeHousingRepository(world, [npc, family], [tile]);
        var system = new NpcHousingSimulationSystem(repository, new NpcHousingOptions());
        var instant = now.AddHours(1);
        var context = new SimulationTickContext(world.Id, new WorldClockSnapshot(world.Id, instant, TimeSpan.FromMinutes(1), 1m, instant));

        await system.ExecuteAsync(context);
        var construction = Assert.Single(repository.Constructions);
        Assert.Equal(50, construction.Progress);
        Assert.Equal(construction.Id, tile.StructureId);
        Assert.Contains(family.Id, construction.ResidentActorIds);
        Assert.Contains(npc.Goals, goal => goal.Code == NpcGoalCodes.NeedHouse);

        await system.ExecuteAsync(context with { Clock = context.Clock with { CurrentInstant = instant.AddHours(1) } });

        Assert.Equal(HousingConstructionStatus.Completed, construction.Status);
        Assert.Equal(construction.Position, npc.Home);
        Assert.Equal(construction.Id, npc.HomeStructureId);
        Assert.Equal(construction.Position, family.Home);
        Assert.Equal(construction.Id, family.HomeStructureId);
        Assert.DoesNotContain(npc.Goals, goal => goal.Code == NpcGoalCodes.NeedHouse);
    }

    [Fact]
    public async Task Missing_resources_or_valid_area_postpones_construction_but_keeps_goal()
    {
        var now = DateTimeOffset.UnixEpoch;
        var world = World.Create("Postponed housing", 8, 8);
        var poor = NpcActor.Create("Poor", world, world.PositionAt(1, 1), now);
        var blocked = NpcActor.Create("Blocked", world, world.PositionAt(2, 2), now);
        blocked.AddInventory("wood", 4, now);
        blocked.AddInventory("stone", 2, now);
        var water = world.CreateTile(world.PositionAt(3, 3), "ocean", Definitions, 0, 20m, 0.5m);
        var repository = new FakeHousingRepository(world, [poor, blocked], [water]);
        var system = new NpcHousingSimulationSystem(repository, new NpcHousingOptions());
        var context = new SimulationTickContext(world.Id, new WorldClockSnapshot(world.Id, now, TimeSpan.FromMinutes(1), 1m, now));

        await system.ExecuteAsync(context);

        Assert.Empty(repository.Constructions);
        Assert.All([poor, blocked], npc => Assert.Contains(npc.Goals, goal => goal.Code == NpcGoalCodes.NeedHouse));
        Assert.Null(water.StructureId);
    }

    [Fact]
    public async Task Multiple_builders_reserve_distinct_tiles_in_the_same_cycle()
    {
        var now = DateTimeOffset.UnixEpoch;
        var world = World.Create("Reserved housing", 8, 8);
        var first = NpcActor.Create("First", world, world.PositionAt(1, 1), now);
        var second = NpcActor.Create("Second", world, world.PositionAt(1, 1), now);
        foreach (var npc in new[] { first, second })
        {
            npc.AddInventory("wood", 4, now);
            npc.AddInventory("stone", 2, now);
        }
        var tiles = new[]
        {
            world.CreateTile(world.PositionAt(2, 1), "grassland", Definitions, 0, 20m, 0.5m),
            world.CreateTile(world.PositionAt(1, 2), "grassland", Definitions, 0, 20m, 0.5m)
        };
        var repository = new FakeHousingRepository(world, [first, second], tiles);
        var system = new NpcHousingSimulationSystem(repository, new NpcHousingOptions());
        var context = new SimulationTickContext(
            world.Id,
            new WorldClockSnapshot(world.Id, now, TimeSpan.FromMinutes(1), 1m, now));

        await system.ExecuteAsync(context);

        Assert.Equal(2, repository.Constructions.Count);
        Assert.Equal(2, repository.Constructions.Select(construction => construction.Position).Distinct().Count());
        Assert.All(tiles, tile => Assert.NotNull(tile.StructureId));
    }

    private sealed class FakeHousingRepository(World world, NpcActor[] npcs, Tile[] tiles) : INpcHousingRepository
    {
        public List<HousingConstruction> Constructions { get; } = [];
        public Task<World?> GetWorldAsync(Guid worldId, CancellationToken cancellationToken = default) => Task.FromResult<World?>(worldId == world.Id ? world : null);
        public Task<IReadOnlyList<NpcActor>> ListHomelessAsync(Guid worldId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<NpcActor>>(npcs.Where(npc => npc.WorldId == worldId && npc.Home is null).ToArray());
        public Task<IReadOnlyList<HousingConstruction>> ListInProgressAsync(Guid worldId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<HousingConstruction>>(Constructions.Where(item => item.WorldId == worldId && item.Status == HousingConstructionStatus.InProgress).ToArray());
        public Task<NpcActor?> GetNpcAsync(Guid actorId, CancellationToken cancellationToken = default) => Task.FromResult(npcs.SingleOrDefault(npc => npc.Id == actorId));
        public Task<Tile?> FindBuildableTileAsync(Guid worldId, int originX, int originY, int radius, IReadOnlyCollection<string> allowedTerrains, CancellationToken cancellationToken = default) => Task.FromResult(tiles.Where(tile => tile.WorldId == worldId && tile.StructureId is null && allowedTerrains.Contains(tile.TerrainCode)).OrderBy(tile => Math.Abs(tile.X - originX) + Math.Abs(tile.Y - originY)).FirstOrDefault());
        public Task<Tile?> GetTileAsync(Position position, CancellationToken cancellationToken = default) => Task.FromResult(tiles.SingleOrDefault(tile => tile.Position == position));
        public void Add(HousingConstruction construction) => Constructions.Add(construction);
        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}

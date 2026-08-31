using RpgWorld.Application.Worlds.Cities;
using RpgWorld.Domain.Actors;
using RpgWorld.Domain.Worlds;
using RpgWorld.Domain.Worlds.Cities;
using RpgWorld.Domain.Worlds.Definitions;

namespace RpgWorld.Infrastructure.Tests.Worlds;

public sealed class CityServiceTests
{
    private static readonly WorldDefinitionCatalog Definitions = new(
        [new TerrainDefinition("plains", "Plains", 1m, true, false)],
        [new BiomeDefinition("grassland", "Grassland", "plains", -10m, 40m, 0m, 1m)]);

    [Fact]
    public async Task Creates_city_on_persisted_tiles_and_associates_initial_resident()
    {
        var now = DateTimeOffset.UnixEpoch;
        var world = World.Create("City service", 8, 8);
        var tiles = CreateTiles(world, (1, 1), (2, 1));
        var npc = NpcActor.Create("Citizen", world, world.PositionAt(1, 1), now);
        var repository = new FakeCityRepository(world, tiles, [npc]);
        var service = new CityService(repository);

        var city = await service.CreateAsync(new CreateCityRequest(
            world.Id,
            "Rivercross",
            1,
            1,
            [new(1, 1), new(2, 1)],
            InitialPopulation: 10,
            InitialWealth: 75m,
            FoundedAtUtc: now,
            ResidentActorIds: [npc.Id]));

        Assert.Equal(10, city.Population);
        Assert.Equal(npc.Id, Assert.Single(city.ResidentActorIds));
        Assert.Equal(city.CityId, npc.ResidentCityId);
        Assert.Equal(2, city.Territory.Count);
    }

    [Fact]
    public async Task Rejects_overlapping_city_territory()
    {
        var world = World.Create("Overlap", 8, 8);
        var tiles = CreateTiles(world, (1, 1), (2, 1), (3, 1));
        var repository = new FakeCityRepository(world, tiles, []);
        var service = new CityService(repository);
        await service.CreateAsync(Request(world, "First", (1, 1), [(1, 1), (2, 1)]));

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAsync(
            Request(world, "Second", (3, 1), [(2, 1), (3, 1)])));
    }

    [Fact]
    public async Task Destruction_detaches_residents_and_preserves_city_history_for_master_query()
    {
        var now = DateTimeOffset.UnixEpoch;
        var world = World.Create("Destroyed", 8, 8);
        var tiles = CreateTiles(world, (1, 1));
        var npc = NpcActor.Create("Citizen", world, world.PositionAt(1, 1), now);
        var repository = new FakeCityRepository(world, tiles, [npc]);
        var service = new CityService(repository);
        var created = await service.CreateAsync(new CreateCityRequest(
            world.Id, "Last Home", 1, 1, [new(1, 1)], 1, 0m, now, ResidentActorIds: [npc.Id]));

        var destroyed = await service.DestroyAsync(
            created.CityId, "Dragon attack", now.AddHours(1));

        Assert.Equal(CityStatus.Destroyed.ToString(), destroyed.Status);
        Assert.Null(npc.ResidentCityId);
        Assert.Empty(destroyed.ResidentActorIds);
        Assert.Equal(CityHistoryEventTypes.Destroyed, destroyed.History[^1].EventType);
        var queried = await service.GetAsync(created.CityId);
        Assert.NotNull(queried);
        Assert.Equal(destroyed.CityId, queried.CityId);
        Assert.Equal(CityStatus.Destroyed.ToString(), queried.Status);
        Assert.Equal(destroyed.History.Select(entry => entry.EventType), queried.History.Select(entry => entry.EventType));
    }

    private static CreateCityRequest Request(
        World world,
        string name,
        (int X, int Y) center,
        (int X, int Y)[] territory) => new(
            world.Id,
            name,
            center.X,
            center.Y,
            territory.Select(cell => new CityTerritoryPosition(cell.X, cell.Y)).ToArray(),
            0,
            0m,
            DateTimeOffset.UnixEpoch);

    private static Tile[] CreateTiles(World world, params (int X, int Y)[] positions) =>
        positions.Select(cell => world.CreateTile(
            world.PositionAt(cell.X, cell.Y), "grassland", Definitions, 0, 20m, 0.5m)).ToArray();

    private sealed class FakeCityRepository(World world, Tile[] tiles, NpcActor[] npcs) : ICityRepository
    {
        private readonly List<City> _cities = [];
        public Task<World?> GetWorldAsync(Guid worldId, CancellationToken cancellationToken = default) => Task.FromResult<World?>(worldId == world.Id ? world : null);
        public Task<City?> GetAsync(Guid cityId, CancellationToken cancellationToken = default) => Task.FromResult(_cities.SingleOrDefault(city => city.Id == cityId));
        public Task<IReadOnlyList<City>> ListByWorldAsync(Guid worldId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<City>>(_cities.Where(city => city.WorldId == worldId).ToArray());
        public Task<NpcActor?> GetNpcAsync(Guid actorId, CancellationToken cancellationToken = default) => Task.FromResult(npcs.SingleOrDefault(npc => npc.Id == actorId));
        public Task<IReadOnlyList<NpcActor>> ListResidentsAsync(Guid cityId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<NpcActor>>(npcs.Where(npc => npc.ResidentCityId == cityId).ToArray());
        public Task<IReadOnlyList<Tile>> ListTilesAsync(Guid worldId, IReadOnlyCollection<Position> positions, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<Tile>>(tiles.Where(tile => positions.Contains(tile.Position)).ToArray());
        public Task<bool> TerritoryOverlapsAsync(Guid worldId, IReadOnlyCollection<Position> positions, CancellationToken cancellationToken = default) => Task.FromResult(_cities.SelectMany(city => city.Territory).Any(positions.Contains));
        public void Add(City city) => _cities.Add(city);
        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}

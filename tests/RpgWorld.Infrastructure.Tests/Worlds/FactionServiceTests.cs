using RpgWorld.Application.Worlds.Factions;
using RpgWorld.Domain.Actors;
using RpgWorld.Domain.Worlds;
using RpgWorld.Domain.Worlds.Cities;
using RpgWorld.Domain.Worlds.Definitions;
using RpgWorld.Domain.Worlds.Factions;

namespace RpgWorld.Infrastructure.Tests.Worlds;

public sealed class FactionServiceTests
{
    private static readonly WorldDefinitionCatalog Definitions = new(
        [new TerrainDefinition("plains", "Plains", 1m, true, false)],
        [new BiomeDefinition("grassland", "Grassland", "plains", -10m, 40m, 0m, 1m)]);

    [Fact]
    public async Task Creates_faction_and_synchronizes_leader_and_territory()
    {
        var now = DateTimeOffset.UnixEpoch;
        var world = World.Create("Faction service", 8, 8);
        var tiles = CreateTiles(world, (1, 1), (2, 1));
        var leader = NpcActor.Create("Leader", world, world.PositionAt(1, 1), now);
        var repository = new FakeFactionRepository(world, tiles, [leader]);
        var service = new FactionService(repository);

        var faction = await service.CreateAsync(new CreateFactionRequest(
            world.Id, "River Crown", FactionType.Kingdom, leader.Id, 100m, 25m, now,
            [new(1, 1), new(2, 1)]));

        Assert.Equal(FactionType.Kingdom.ToString(), faction.Type);
        Assert.Equal(leader.Id, faction.LeaderActorId);
        Assert.Equal(faction.FactionId, leader.FactionId);
        Assert.Equal(2, faction.Territory.Count);
        Assert.Equal(1, repository.SaveCount);
    }

    [Fact]
    public async Task Adds_member_changes_leader_then_allows_former_leader_to_leave()
    {
        var now = DateTimeOffset.UnixEpoch;
        var world = World.Create("Succession", 8, 8);
        var tiles = CreateTiles(world, (1, 1));
        var founder = NpcActor.Create("Founder", world, world.PositionAt(1, 1), now);
        var successor = NpcActor.Create("Successor", world, world.PositionAt(1, 1), now);
        var repository = new FakeFactionRepository(world, tiles, [founder, successor]);
        var service = new FactionService(repository);
        var created = await service.CreateAsync(new CreateFactionRequest(
            world.Id, "Order", FactionType.Guild, founder.Id, 0m, 0m, now));

        await service.AddMemberAsync(created.FactionId, successor.Id, now.AddHours(1));
        await service.ChangeLeaderAsync(
            created.FactionId, successor.Id, "Elected by members.", now.AddHours(2));
        var changed = await service.RemoveMemberAsync(
            created.FactionId, founder.Id, "Founder retired.", now.AddHours(3));

        Assert.Equal(successor.Id, changed.LeaderActorId);
        Assert.Equal([successor.Id], changed.MemberActorIds);
        Assert.Null(founder.FactionId);
        Assert.Equal(created.FactionId, successor.FactionId);
    }

    [Fact]
    public async Task Associates_city_and_dissolution_detaches_all_links_but_keeps_history()
    {
        var now = DateTimeOffset.UnixEpoch;
        var world = World.Create("Political cities", 8, 8);
        var tiles = CreateTiles(world, (1, 1), (2, 1));
        var leader = NpcActor.Create("Leader", world, world.PositionAt(1, 1), now);
        var member = NpcActor.Create("Member", world, world.PositionAt(2, 1), now);
        var city = City.Create(
            world, "Capital", world.PositionAt(1, 1), [world.PositionAt(1, 1), world.PositionAt(2, 1)],
            10, 100m, now);
        var repository = new FakeFactionRepository(world, tiles, [leader, member], [city]);
        var service = new FactionService(repository);
        var created = await service.CreateAsync(new CreateFactionRequest(
            world.Id, "Tribe", FactionType.Tribe, leader.Id, 0m, 5m, now));
        await service.AddMemberAsync(created.FactionId, member.Id, now.AddHours(1));
        var associated = await service.AssociateCityAsync(
            created.FactionId, city.Id, claimCityTerritory: true, now.AddHours(2));

        Assert.Equal(city.Id, Assert.Single(associated.ControlledCityIds));
        Assert.Equal(created.FactionId, city.GoverningFactionId);
        Assert.Equal(2, associated.Territory.Count);

        var dissolved = await service.DissolveAsync(
            created.FactionId, "The tribe split.", now.AddHours(3));

        Assert.Equal(FactionStatus.Dissolved.ToString(), dissolved.Status);
        Assert.Null(leader.FactionId);
        Assert.Null(member.FactionId);
        Assert.Null(city.GoverningFactionId);
        Assert.Empty(dissolved.Territory);
        Assert.Equal(FactionHistoryEventTypes.Dissolved, dissolved.History[^1].EventType);
    }

    [Fact]
    public async Task Game_master_prevents_then_forces_war_through_application_service()
    {
        var now = DateTimeOffset.UnixEpoch;
        var world = World.Create("War command", 8, 8);
        var firstLeader = NpcActor.Create("First leader", world, world.PositionAt(1, 1), now);
        var secondLeader = NpcActor.Create("Second leader", world, world.PositionAt(2, 1), now);
        var repository = new FakeFactionRepository(
            world, CreateTiles(world, (1, 1), (2, 1)), [firstLeader, secondLeader]);
        var service = new FactionService(repository);
        var first = await service.CreateAsync(new CreateFactionRequest(
            world.Id, "First", FactionType.Kingdom, firstLeader.Id, 0m, 10m, now));
        var second = await service.CreateAsync(new CreateFactionRequest(
            world.Id, "Second", FactionType.Kingdom, secondLeader.Id, 0m, 10m, now));

        var prevented = await service.PreventWarAsync(
            first.FactionId, second.FactionId, now.AddDays(1), "Hold the conflict.", now.AddHours(1));
        Assert.Equal(now.AddDays(1), Assert.Single(prevented.Relations).WarPreventedUntilUtc);

        var forced = await service.ForceWarAsync(
            first.FactionId, second.FactionId, "Begin the campaign.", now.AddHours(2));
        Assert.Equal(FactionRelationKind.War.ToString(), Assert.Single(forced.Relations).State);
        Assert.Equal(FactionHistoryEventTypes.WarDeclared, forced.History[^1].EventType);
    }

    private static Tile[] CreateTiles(World world, params (int X, int Y)[] positions) =>
        positions.Select(cell => world.CreateTile(
            world.PositionAt(cell.X, cell.Y), "grassland", Definitions, 0, 20m, 0.5m)).ToArray();

    private sealed class FakeFactionRepository(
        World world,
        Tile[] tiles,
        Actor[] actors,
        City[]? initialCities = null) : IFactionRepository
    {
        private readonly List<Faction> _factions = [];
        private readonly City[] _cities = initialCities ?? [];
        public int SaveCount { get; private set; }

        public Task<World?> GetWorldAsync(Guid worldId, CancellationToken cancellationToken = default) =>
            Task.FromResult<World?>(worldId == world.Id ? world : null);
        public Task<Faction?> GetAsync(Guid factionId, CancellationToken cancellationToken = default) =>
            Task.FromResult(_factions.SingleOrDefault(faction => faction.Id == factionId));
        public Task<IReadOnlyList<Faction>> ListByWorldAsync(Guid worldId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Faction>>(_factions.Where(faction => faction.WorldId == worldId).ToArray());
        public Task<bool> NameExistsAsync(Guid worldId, string name, CancellationToken cancellationToken = default) =>
            Task.FromResult(_factions.Any(faction => faction.WorldId == worldId && faction.Name == name));
        public Task<Actor?> GetActorAsync(Guid actorId, CancellationToken cancellationToken = default) =>
            Task.FromResult(actors.SingleOrDefault(actor => actor.Id == actorId));
        public Task<IReadOnlyList<Actor>> ListMembersAsync(Guid factionId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Actor>>(actors.Where(actor => actor.FactionId == factionId).ToArray());
        public Task<City?> GetCityAsync(Guid cityId, CancellationToken cancellationToken = default) =>
            Task.FromResult(_cities.SingleOrDefault(city => city.Id == cityId));
        public Task<IReadOnlyList<City>> ListCitiesAsync(Guid factionId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<City>>(_cities.Where(city => city.GoverningFactionId == factionId).ToArray());
        public Task<IReadOnlyList<Tile>> ListTilesAsync(Guid worldId, IReadOnlyCollection<Position> positions, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Tile>>(tiles.Where(tile => positions.Contains(tile.Position)).ToArray());
        public Task<bool> TerritoryOverlapsAsync(Guid worldId, IReadOnlyCollection<Position> positions, Guid? excludingFactionId = null, CancellationToken cancellationToken = default) =>
            Task.FromResult(_factions.Where(faction => faction.Id != excludingFactionId)
                .SelectMany(faction => faction.Territory).Any(positions.Contains));
        public void Add(Faction faction) => _factions.Add(faction);
        public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            SaveCount++;
            return Task.CompletedTask;
        }
    }
}

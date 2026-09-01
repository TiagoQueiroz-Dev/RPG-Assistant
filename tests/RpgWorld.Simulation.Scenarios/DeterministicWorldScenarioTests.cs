using RpgWorld.Application.Actors;
using RpgWorld.Application.Worlds.Cities;
using RpgWorld.Application.Worlds.Factions;
using RpgWorld.Application.Worlds.Resources;
using RpgWorld.Domain.Actors;
using RpgWorld.Domain.Worlds;
using RpgWorld.Domain.Worlds.Cities;
using RpgWorld.Domain.Worlds.Definitions;
using RpgWorld.Domain.Worlds.Factions;
using RpgWorld.Domain.Worlds.Resources;
using RpgWorld.Simulation.Actors;
using RpgWorld.Simulation.Actors.Utility;
using RpgWorld.Simulation.Engine;
using RpgWorld.Simulation.Regions;
using RpgWorld.Simulation.Time;
using RpgWorld.Simulation.Worlds.Economy;
using RpgWorld.Simulation.Worlds.Factions;
using RpgWorld.Simulation.Worlds.Resources;
using RpgWorld.Testing;

namespace RpgWorld.Simulation.Scenarios;

public sealed class DeterministicWorldScenarioTests
{
    private static readonly WorldDefinitionCatalog Definitions = new(
        [new TerrainDefinition("plains", "Plains", 1m, true, false, ["food"])],
        [new BiomeDefinition("grassland", "Grassland", "plains", -10m, 40m, 0m, 1m)],
        [new ResourceDefinition("food", "Food", "food", 100m, 2m, ["food"])]);

    [Fact]
    public async Task Same_clock_and_seed_reproduce_complete_world_outcome()
    {
        var first = await RunScenarioAsync(seed: 7331);
        var second = await RunScenarioAsync(seed: 7331);

        Assert.Equal(first, second);
        Assert.Equal(DateTimeOffset.UnixEpoch.AddHours(6), first.WorldInstant);
        Assert.Equal(12m, first.RenewableResourceQuantity);
        Assert.Equal(CityMarketCondition.Shortage, first.FoodMarketCondition);
        Assert.Equal(FactionRelationKind.War, first.DiplomaticState);
        Assert.Equal((SimulationLevel.Detailed, SimulationLevel.Regional),
            (first.PlayerRegionLevel, first.NeighborRegionLevel));
        Assert.Contains("CityResourceShortageEvent", first.EventTypes);
        Assert.Contains("FactionWarDeclaredEvent", first.EventTypes);
    }

    private static async Task<ScenarioOutcome> RunScenarioAsync(int seed)
    {
        var start = DateTimeOffset.UnixEpoch;
        var time = new DeterministicTimeProvider(start);
        var world = World.Create("Repeatable world", 64, 64);
        var clock = WorldClock.Create(world.Id, start, time.GetUtcNow(), TimeSpan.FromHours(1));
        time.Advance(TimeSpan.FromHours(6));
        clock.AdvanceTicks(6);
        var snapshot = new WorldClockSnapshot(
            world.Id, clock.CurrentInstant, clock.TickDuration, clock.RealTimeMultiplier, time.GetUtcNow());
        var tick = new SimulationTickContext(world.Id, snapshot, new SimulationTickWorkload(2));

        var npc = NpcActor.Create("Seeded villager", world, world.PositionAt(1, 1), start);
        await new NpcNeedsSimulationSystem(new NpcRepository(npc)).ExecuteAsync(tick);
        var random = new SeededSimulationRandom(seed);
        var utility = new NpcUtilityDecisionService(
            [new ConstantAction("explore-east"), new ConstantAction("explore-west")],
            new UtilityAiOptions(), [], random);
        var decisionContext = new NpcDecisionContext(npc, 0m, 1m, 1m, false, 0m);
        var decisions = Enumerable.Range(0, 8)
            .Select(_ => utility.Decide(decisionContext)!.ActionCode).ToArray();

        var resolver = new SimulationLevelResolver(new SimulationLevelOptions(0, 1));
        var playerRegion = resolver.Resolve(world, new ChunkCoordinate(0, 0), [world.PositionAt(1, 1)]);
        var neighborRegion = resolver.Resolve(world, new ChunkCoordinate(1, 0), [world.PositionAt(1, 1)]);

        var tile = world.CreateTile(world.PositionAt(2, 2), "grassland", Definitions, 0, 20m, 0.5m);
        var deposit = ResourceDeposit.SpawnOnTile(
            world, tile, Definitions.ResolveResource("food"), start, initialQuantity: 0m);
        deposit.ClearDomainEvents();
        await new NaturalResourceRegenerationSystem(new ResourceRepository(world, tile, deposit)).ExecuteAsync(tick);

        var cityPosition = world.PositionAt(3, 3);
        var city = City.Create(world, "Seeded city", cityPosition, [cityPosition], 10, 0m, start);
        city.ClearDomainEvents();
        var economyOptions = new CityEconomyOptions
        {
            Resources =
            [
                new CityEconomyResourceOptions
                {
                    ResourceCode = "food", NaturalResourceCode = "food",
                    NaturalExtractionPerResident = 1m, ConsumptionPerResident = 1m,
                    BasePrice = 2m, TargetStockPerResident = 2m
                }
            ]
        };
        await new CityEconomySimulationSystem(new EconomyRepository(city), economyOptions).ExecuteAsync(tick);

        var north = Faction.Create(world, "North", FactionType.Kingdom,
            Guid.Parse("10000000-0000-0000-0000-000000000001"), 0m, 100m, start);
        var south = Faction.Create(world, "South", FactionType.Kingdom,
            Guid.Parse("20000000-0000-0000-0000-000000000002"), 0m, 20m, start);
        north.ClearDomainEvents();
        south.ClearDomainEvents();
        var warRepository = new WarRepository([north, south]);
        await new FactionWarDeclarationSimulationSystem(warRepository,
            new WarScoreCalculator(new WarDeclarationOptions { DeclareWarThreshold = 60m })).ExecuteAsync(tick);

        var eventTypes = city.DomainEvents.Concat(north.DomainEvents).Concat(south.DomainEvents)
            .Select(value => value.GetType().Name).Order(StringComparer.Ordinal).ToArray();
        return new ScenarioOutcome(clock.CurrentInstant, npc.Hunger, npc.Energy, string.Join(',', decisions),
            playerRegion, neighborRegion, deposit.Quantity, city.ResourceMarkets["food"].Condition,
            city.ResourceMarkets["food"].UnitPrice, north.Relations[south.Id].Kind,
            north.Relations[south.Id].LastWarScore!.Total, string.Join(',', eventTypes));
    }

    private sealed record ScenarioOutcome(
        DateTimeOffset WorldInstant,
        decimal NpcHunger,
        decimal NpcEnergy,
        string Decisions,
        SimulationLevel PlayerRegionLevel,
        SimulationLevel NeighborRegionLevel,
        decimal RenewableResourceQuantity,
        CityMarketCondition FoodMarketCondition,
        decimal FoodUnitPrice,
        FactionRelationKind DiplomaticState,
        decimal WarScore,
        string EventTypes);

    private sealed class ConstantAction(string code) : NpcAction(
        code, new UtilityConsideration("constant", _ => 0.5m))
    {
        public override NpcActionEligibility CheckEligibility(NpcDecisionContext context) =>
            NpcActionEligibility.Eligible;
    }

    private sealed class NpcRepository(NpcActor npc) : INpcNeedsRepository
    {
        public Task<IReadOnlyList<NpcActor>> ListForUpdateAsync(Guid worldId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<NpcActor>>(npc.WorldId == worldId ? [npc] : []);
        public Task<IReadOnlyList<NpcNeedsSnapshot>> ListUrgentAsync(Guid worldId, decimal minimumHunger,
            decimal maximumEnergy, int limit = 100, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class ResourceRepository(World world, Tile tile, ResourceDeposit deposit) : INaturalResourceRepository
    {
        public Task<World?> GetWorldAsync(Guid worldId, CancellationToken cancellationToken = default) => Task.FromResult<World?>(world);
        public Task<Tile?> GetTileAsync(Position position, CancellationToken cancellationToken = default) => Task.FromResult<Tile?>(tile);
        public Task<Actor?> GetActorAsync(Guid actorId, CancellationToken cancellationToken = default) => Task.FromResult<Actor?>(null);
        public Task<ResourceDeposit?> GetDepositAsync(Guid depositId, CancellationToken cancellationToken = default) => Task.FromResult<ResourceDeposit?>(deposit);
        public Task<IReadOnlyList<ResourceDeposit>> ListAvailableInRegionAsync(Guid worldId, ChunkCoordinate region,
            IReadOnlyCollection<string>? resourceCodes = null, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ResourceDeposit>>([deposit]);
        public Task<IReadOnlyList<ResourceDeposit>> ListRegeneratingAsync(Guid worldId, DateTimeOffset worldInstant,
            CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<ResourceDeposit>>([deposit]);
        public void Add(ResourceDeposit value) { }
        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class EconomyRepository(City city) : ICityEconomyRepository
    {
        public Task<IReadOnlyList<City>> ListSimulatedCitiesAsync(Guid worldId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<City>>(city.WorldId == worldId ? [city] : []);
        public Task<IReadOnlyList<ResourceDeposit>> ListAvailableDepositsAsync(City candidate,
            IReadOnlyCollection<string> resourceCodes, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ResourceDeposit>>([]);
        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class WarRepository(IReadOnlyList<Faction> factions) : IFactionWarRepository
    {
        public Task<IReadOnlyList<Faction>> ListActiveAsync(Guid worldId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Faction>>(factions.Where(value => value.WorldId == worldId).ToArray());
        public Task<FactionWarContext> BuildContextAsync(Faction source, Faction target,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new FactionWarContext(5, 3, 500m, true));
        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}

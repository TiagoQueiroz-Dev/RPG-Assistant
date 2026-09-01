using RpgWorld.Application.Worlds.Cities;
using RpgWorld.Domain.Actors;
using RpgWorld.Domain.Worlds;
using RpgWorld.Domain.Worlds.Cities;
using RpgWorld.Domain.Worlds.Definitions;
using RpgWorld.Domain.Worlds.Resources;
using RpgWorld.Simulation.Engine;
using RpgWorld.Simulation.Time;
using RpgWorld.Simulation.Worlds.Economy;

namespace RpgWorld.Simulation.Tests.Worlds;

public sealed class CityEconomySimulationSystemTests
{
    private static readonly WorldDefinitionCatalog Definitions = new(
        [new TerrainDefinition("plains", "Plains", 1m, true, false, ["food"])],
        [new BiomeDefinition("grassland", "Grassland", "plains", -10m, 40m, 0m, 1m)],
        [new ResourceDefinition("food", "Food", "food", 100m, habitatTags: ["food"])]);

    [Fact]
    public async Task Cycle_extracts_local_resources_consumes_needs_and_applies_building_output()
    {
        var now = DateTimeOffset.UnixEpoch;
        var world = World.Create("Economic world", 8, 8);
        var position = world.PositionAt(1, 1);
        var tile = world.CreateTile(position, "grassland", Definitions, 0, 20m, 0.5m);
        var city = City.Create(world, "Harvest", position, [position], 10, 0m, now);
        city.AddBuilding(Guid.NewGuid(), now.AddMinutes(1));
        var deposit = ResourceDeposit.SpawnOnTile(
            world, tile, Definitions.ResolveResource("food"), now, initialQuantity: 50m);
        deposit.Discover(Guid.NewGuid(), now.AddMinutes(1));
        city.ClearDomainEvents();
        deposit.ClearDomainEvents();
        var repository = new FakeCityEconomyRepository(city, deposit);
        var options = new CityEconomyOptions
        {
            Resources =
            [
                new CityEconomyResourceOptions
                {
                    ResourceCode = "food",
                    NaturalResourceCode = "food",
                    NaturalExtractionPerResident = 1m,
                    ProductionPerBuilding = 5m,
                    ConsumptionPerResident = 1m,
                    BasePrice = 2m,
                    TargetStockPerResident = 2m
                }
            ]
        };
        options.Validate();
        var system = new CityEconomySimulationSystem(repository, options);
        var instant = now.AddHours(1);

        await system.ExecuteAsync(new SimulationTickContext(
            world.Id,
            new WorldClockSnapshot(world.Id, instant, TimeSpan.FromHours(1), 1m, instant)));

        var market = city.ResourceMarkets["food"];
        Assert.Equal(15m, market.Produced);
        Assert.Equal(10m, market.Consumed);
        Assert.Equal(5m, city.ResourceStocks["food"]);
        Assert.Equal(40m, deposit.Quantity);
        Assert.Equal(ResourceConsumerKind.City, deposit.LastConsumerKind);
        Assert.Equal(city.Id, deposit.LastConsumerId);
        Assert.Equal(1, repository.SaveCount);
    }

    [Fact]
    public async Task Cycle_without_supply_demonstrates_critical_supply_crisis()
    {
        var now = DateTimeOffset.UnixEpoch;
        var world = World.Create("Scarcity", 4, 4);
        var position = world.PositionAt(0, 0);
        var city = City.Create(world, "Hungry", position, [position], 20, 0m, now);
        city.ClearDomainEvents();
        var repository = new FakeCityEconomyRepository(city);
        var options = new CityEconomyOptions
        {
            Resources =
            [
                new CityEconomyResourceOptions
                {
                    ResourceCode = "food",
                    NaturalResourceCode = "food",
                    NaturalExtractionPerResident = 1m,
                    ConsumptionPerResident = 1m,
                    BasePrice = 2m,
                    TargetStockPerResident = 2m
                }
            ]
        };
        options.Validate();

        await new CityEconomySimulationSystem(repository, options).ExecuteAsync(new SimulationTickContext(
            world.Id,
            new WorldClockSnapshot(world.Id, now.AddHours(1), TimeSpan.FromHours(1), 1m, now)));

        var market = city.ResourceMarkets["food"];
        Assert.Equal(20m, market.UnmetDemand);
        Assert.Equal(CityMarketCondition.Shortage, market.Condition);
        Assert.Equal(8m, market.UnitPrice);
        Assert.Contains(city.DomainEvents, value => value is RpgWorld.Domain.Events.CityResourceShortageEvent);
        Assert.Contains(city.History, value => value.EventType == CityHistoryEventTypes.ResourceShortage);
    }

    private sealed class FakeCityEconomyRepository(City city, params ResourceDeposit[] deposits)
        : ICityEconomyRepository
    {
        public int SaveCount { get; private set; }

        public Task<IReadOnlyList<City>> ListSimulatedCitiesAsync(
            Guid worldId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<City>>(city.WorldId == worldId ? [city] : []);

        public Task<IReadOnlyList<ResourceDeposit>> ListAvailableDepositsAsync(
            City candidate,
            IReadOnlyCollection<string> resourceCodes,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ResourceDeposit>>(deposits
                .Where(deposit => resourceCodes.Contains(deposit.ResourceCode)).ToArray());

        public Task<IReadOnlyList<NpcActor>> ListActiveMerchantsAsync(
            City candidate,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<NpcActor>>([]);

        public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            SaveCount++;
            return Task.CompletedTask;
        }
    }
}

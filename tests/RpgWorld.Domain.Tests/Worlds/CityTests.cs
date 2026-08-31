using RpgWorld.Domain.Actors;
using RpgWorld.Domain.Events;
using RpgWorld.Domain.Worlds;
using RpgWorld.Domain.Worlds.Cities;

namespace RpgWorld.Domain.Tests.Worlds;

public sealed class CityTests
{
    [Fact]
    public void Creates_city_with_contiguous_territory_and_foundation_history()
    {
        var world = World.Create("Cities", 8, 8);
        var center = world.PositionAt(0, 0);
        var city = City.Create(
            world,
            "Northwatch",
            center,
            [center, world.PositionAt(1, 0), world.PositionAt(1, 1)],
            initialPopulation: 12,
            initialWealth: 500m,
            DateTimeOffset.UnixEpoch);

        Assert.Equal(center, city.Center);
        Assert.Equal(3, city.Territory.Count);
        Assert.Equal(12, city.Population);
        Assert.Equal(500m, city.Wealth);
        Assert.Equal(CityHistoryEventTypes.Founded, Assert.Single(city.History).EventType);
        Assert.IsType<CityCreatedEvent>(Assert.Single(city.DomainEvents));
    }

    [Fact]
    public void Rejects_disconnected_or_centerless_territory()
    {
        var world = World.Create("Invalid territory", 8, 8);
        var center = world.PositionAt(1, 1);

        Assert.Throws<ArgumentException>(() => City.Create(
            world, "Disconnected", center, [center, world.PositionAt(4, 4)], 1, 0m, DateTimeOffset.UnixEpoch));
        Assert.Throws<ArgumentException>(() => City.Create(
            world, "Centerless", center, [world.PositionAt(1, 2)], 1, 0m, DateTimeOffset.UnixEpoch));
    }

    [Fact]
    public void Residents_and_population_grow_and_decline_together()
    {
        var now = DateTimeOffset.UnixEpoch;
        var (world, city) = CreateCity(initialPopulation: 5);
        var npc = NpcActor.Create("Resident", world, city.Center, now);
        city.ClearDomainEvents();

        Assert.True(city.AddResident(npc.Id, now.AddHours(1)));
        npc.JoinCity(city, now.AddHours(1));
        Assert.Equal(6, city.Population);
        Assert.Equal(city.Id, npc.ResidentCityId);
        Assert.IsType<CityGrowthEvent>(Assert.Single(city.DomainEvents));

        city.ClearDomainEvents();
        Assert.True(city.RemoveResident(npc.Id, "Resident migrated.", now.AddHours(2)));
        npc.LeaveCity(city.Id, now.AddHours(2));
        Assert.Equal(5, city.Population);
        Assert.Null(npc.ResidentCityId);
        Assert.IsType<CityPopulationChangedEvent>(Assert.Single(city.DomainEvents));
    }

    [Fact]
    public void Tracks_resources_buildings_wealth_and_political_link()
    {
        var now = DateTimeOffset.UnixEpoch;
        var (_, city) = CreateCity();
        var buildingId = Guid.NewGuid();
        var factionId = Guid.NewGuid();

        city.StoreResource("food", 20m, now.AddHours(1));
        city.ConsumeResource("FOOD", 3m, now.AddHours(2));
        city.AddBuilding(buildingId, now.AddHours(3));
        city.CreditWealth(25m, now.AddHours(4));
        city.DebitWealth(5m, now.AddHours(5));
        city.SetGoverningFaction(factionId, now.AddHours(6));

        Assert.Equal(17m, city.ResourceStocks["food"]);
        Assert.Equal(buildingId, Assert.Single(city.BuildingIds));
        Assert.Equal(20m, city.Wealth);
        Assert.Equal(factionId, city.GoverningFactionId);
    }

    [Fact]
    public void Crisis_and_destruction_raise_events_and_keep_historical_record()
    {
        var now = DateTimeOffset.UnixEpoch;
        var (_, city) = CreateCity(initialPopulation: 20);
        city.ClearDomainEvents();

        city.BeginCrisis("Food shortage", 80, now.AddHours(1));
        Assert.Equal(CityStatus.Crisis, city.Status);
        Assert.IsType<CityCrisisEvent>(Assert.Single(city.DomainEvents));
        city.ClearDomainEvents();
        city.Destroy("The settlement was abandoned.", now.AddHours(2));

        var destroyed = Assert.IsType<CityDestroyedEvent>(Assert.Single(city.DomainEvents));
        Assert.Equal(20, destroyed.FinalPopulation);
        Assert.Equal(CityStatus.Destroyed, city.Status);
        Assert.Equal(0, city.Population);
        Assert.NotNull(city.DestroyedAtUtc);
        Assert.All(city.TerritoryTiles, tile =>
        {
            Assert.False(tile.IsActive);
            Assert.Equal(city.DestroyedAtUtc, tile.ReleasedAtUtc);
        });
        Assert.Equal([CityHistoryEventTypes.Founded, CityHistoryEventTypes.Crisis, CityHistoryEventTypes.Destroyed],
            city.History.Select(entry => entry.EventType));
        Assert.Equal("20", city.History[^1].Metadata["finalPopulation"]);
        Assert.Throws<InvalidOperationException>(() => city.CreditWealth(1m, now.AddHours(3)));
    }

    [Fact]
    public void Economic_cycles_consume_population_needs_and_change_stock_and_price()
    {
        var now = DateTimeOffset.UnixEpoch;
        var (_, city) = CreateCity(initialPopulation: 10);
        city.StoreResource("food", 5m, now.AddHours(1));
        city.ClearDomainEvents();
        var rule = new CityResourceEconomyRule(
            "food", consumptionPerResident: 1m, basePrice: 2m, targetStockPerResident: 2m);

        var shortage = city.RunEconomicCycle([rule], new Dictionary<string, decimal>(), now.AddHours(2));

        var shortMarket = Assert.Single(shortage.Markets);
        Assert.Equal(5m, shortMarket.OpeningStock);
        Assert.Equal(10m, shortMarket.Demand);
        Assert.Equal(5m, shortMarket.Consumed);
        Assert.Equal(5m, shortMarket.UnmetDemand);
        Assert.Equal(0m, shortMarket.ClosingStock);
        Assert.Equal(8m, shortMarket.UnitPrice);
        Assert.Equal(CityMarketCondition.Shortage, shortMarket.Condition);
        Assert.IsType<CityResourceShortageEvent>(Assert.Single(city.DomainEvents));

        city.ClearDomainEvents();
        var surplus = city.RunEconomicCycle(
            [rule],
            new Dictionary<string, decimal> { ["food"] = 50m },
            now.AddHours(3));

        var surplusMarket = Assert.Single(surplus.Markets);
        Assert.Equal(50m, surplusMarket.Produced);
        Assert.Equal(40m, surplusMarket.ClosingStock);
        Assert.Equal(1m, surplusMarket.UnitPrice);
        Assert.Equal(CityMarketCondition.Surplus, surplusMarket.Condition);
        Assert.IsType<CityResourceSurplusEvent>(Assert.Single(city.DomainEvents));
        Assert.Equal(2, city.EconomicCycleCount);
        Assert.Equal(now.AddHours(3), city.LastEconomicCycleAtUtc);
        Assert.Equal(surplusMarket, city.ResourceMarkets["food"]);
    }

    [Fact]
    public void Economic_rules_are_configurable_and_cycles_cannot_be_replayed()
    {
        var now = DateTimeOffset.UnixEpoch;
        var (_, city) = CreateCity(initialPopulation: 4);
        var customRule = new CityResourceEconomyRule(
            "wood",
            consumptionPerResident: 0.25m,
            basePrice: 12m,
            targetStockPerResident: 0.50m,
            maximumPriceMultiplier: 3m);

        var cycle = city.RunEconomicCycle(
            [customRule],
            new Dictionary<string, decimal> { ["wood"] = 2m },
            now.AddHours(1));

        var market = Assert.Single(cycle.Markets);
        Assert.Equal(1m, market.Demand);
        Assert.Equal(1m, market.ClosingStock);
        Assert.Equal(24m, market.UnitPrice);
        Assert.Throws<ArgumentOutOfRangeException>(() => city.RunEconomicCycle(
            [customRule], new Dictionary<string, decimal>(), now.AddHours(1)));
    }

    private static (World World, City City) CreateCity(int initialPopulation = 0)
    {
        var world = World.Create("City world", 8, 8);
        var center = world.PositionAt(2, 2);
        return (world, City.Create(
            world, "Aster", center, [center, world.PositionAt(3, 2)], initialPopulation, 0m, DateTimeOffset.UnixEpoch));
    }
}

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

    private static (World World, City City) CreateCity(int initialPopulation = 0)
    {
        var world = World.Create("City world", 8, 8);
        var center = world.PositionAt(2, 2);
        return (world, City.Create(
            world, "Aster", center, [center, world.PositionAt(3, 2)], initialPopulation, 0m, DateTimeOffset.UnixEpoch));
    }
}

using RpgWorld.Domain.Events;
using RpgWorld.Domain.Worlds;
using RpgWorld.Domain.Worlds.Definitions;
using RpgWorld.Domain.Worlds.Resources;

namespace RpgWorld.Domain.Tests.Worlds;

public sealed class ResourceDepositTests
{
    private static readonly WorldDefinitionCatalog Definitions = new(
        [new TerrainDefinition("woodland", "Woodland", 1m, true, false, ["wood"])],
        [new BiomeDefinition("forest", "Forest", "woodland", -10m, 40m, 0m, 1m, resourceTags: ["wood"])],
        [new ResourceDefinition("wood", "Wood", "timber", 10m, 2m, ["wood"])]);

    [Fact]
    public void Discovery_and_exhaustion_raise_events_and_collection_reduces_quantity()
    {
        var now = DateTimeOffset.UnixEpoch;
        var (world, tile) = CreateWorldAndTile();
        var deposit = ResourceDeposit.SpawnOnTile(
            world, tile, Definitions.ResolveResource("wood"), now, initialQuantity: 5m,
            regenerationPerWorldHour: 0m);
        var actorId = Guid.NewGuid();
        deposit.ClearDomainEvents();

        Assert.True(deposit.Discover(actorId, now.AddHours(1)));
        var discovered = Assert.IsType<ResourceDiscoveredEvent>(Assert.Single(deposit.DomainEvents));
        Assert.Equal(deposit.Id, discovered.ResourceId);
        deposit.ClearDomainEvents();

        var extraction = deposit.Extract(5m, ResourceConsumer.Actor(actorId), now.AddHours(1));

        Assert.Equal(5m, extraction.Quantity);
        Assert.True(extraction.Exhausted);
        Assert.Equal(0m, deposit.Quantity);
        var exhausted = Assert.IsType<ResourceExhaustedEvent>(Assert.Single(deposit.DomainEvents));
        Assert.Equal(ResourceConsumerKind.Actor, exhausted.ConsumerKind);
        Assert.Equal(actorId, exhausted.ConsumerId);
    }

    [Fact]
    public void Renewable_deposit_regenerates_by_elapsed_world_time_up_to_capacity()
    {
        var now = DateTimeOffset.UnixEpoch;
        var (world, tile) = CreateWorldAndTile();
        var deposit = ResourceDeposit.SpawnOnTile(
            world, tile, Definitions.ResolveResource("wood"), now, initialQuantity: 2m);
        deposit.Discover(Guid.NewGuid(), now);
        deposit.Extract(2m, ResourceConsumer.Construction(Guid.NewGuid()), now);

        Assert.Equal(4m, deposit.RegenerateTo(now.AddHours(2)));
        Assert.Equal(4m, deposit.Quantity);
        Assert.Equal(6m, deposit.RegenerateTo(now.AddHours(20)));
        Assert.Equal(10m, deposit.Quantity);
    }

    [Fact]
    public void Region_deposit_can_spawn_from_world_event_and_support_city_consumption()
    {
        var now = DateTimeOffset.UnixEpoch;
        var world = World.Create("Regional resources", 64, 64);
        var sourceEventId = Guid.NewGuid();
        var deposit = ResourceDeposit.SpawnInRegion(
            world,
            new ChunkCoordinate(1, 1),
            Definitions.ResolveResource("wood"),
            now,
            sourceWorldEventId: sourceEventId);
        var spawned = Assert.IsType<ResourceSpawnedEvent>(Assert.Single(deposit.DomainEvents));
        Assert.Equal(sourceEventId, spawned.SourceWorldEventId);
        deposit.ClearDomainEvents();
        deposit.Discover(Guid.NewGuid(), now);
        deposit.ClearDomainEvents();

        var extraction = deposit.Extract(3m, ResourceConsumer.City(Guid.NewGuid()), now);

        Assert.Equal(ResourceDepositScope.Region, deposit.Scope);
        Assert.Null(deposit.TileId);
        Assert.Equal(sourceEventId, deposit.SourceWorldEventId);
        Assert.Equal(ResourceConsumerKind.City, extraction.Consumer.Kind);
        Assert.Equal(7m, deposit.Quantity);
    }

    [Fact]
    public void Undiscovered_deposit_cannot_be_extracted()
    {
        var (world, tile) = CreateWorldAndTile();
        var deposit = ResourceDeposit.SpawnOnTile(
            world, tile, Definitions.ResolveResource("wood"), DateTimeOffset.UnixEpoch);

        Assert.Throws<InvalidOperationException>(() => deposit.Extract(
            1m, ResourceConsumer.Actor(Guid.NewGuid()), DateTimeOffset.UnixEpoch));
        Assert.Equal(10m, deposit.Quantity);
    }

    private static (World World, Tile Tile) CreateWorldAndTile()
    {
        var world = World.Create("Resources", 8, 8);
        var tile = world.CreateTile(
            world.PositionAt(1, 1), "forest", Definitions, 0, 20m, 0.5m);
        return (world, tile);
    }
}

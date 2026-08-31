using RpgWorld.Application.Worlds.Resources;
using RpgWorld.Domain.Actors;
using RpgWorld.Domain.Worlds;
using RpgWorld.Domain.Worlds.Definitions;
using RpgWorld.Domain.Worlds.Resources;
using RpgWorld.Domain.Events;

namespace RpgWorld.Application.Tests.Worlds;

public sealed class NaturalResourceServiceTests
{
    private static readonly WorldDefinitionCatalog Definitions = new(
        [new TerrainDefinition("woodland", "Woodland", 1m, true, false, ["wood"])],
        [new BiomeDefinition("forest", "Forest", "woodland", -10m, 40m, 0m, 1m, resourceTags: ["wood"])],
        [
            new ResourceDefinition("wood", "Wood", "timber", 20m, 1m, ["wood"]),
            new ResourceDefinition("stone", "Stone", "stone", 20m, habitatTags: ["stone"])
        ]);

    [Fact]
    public async Task Spawns_discovers_and_collects_tile_resource_into_actor_inventory()
    {
        var now = DateTimeOffset.UnixEpoch;
        var world = World.Create("Resource service", 8, 8);
        var tile = world.CreateTile(world.PositionAt(2, 2), "forest", Definitions, 0, 20m, 0.5m);
        var actor = NpcActor.Create("Gatherer", world, tile.Position, now);
        var repository = new FakeNaturalResourceRepository(world, [tile], [actor]);
        var service = new NaturalResourceService(repository, Definitions);

        var spawned = await service.SpawnOnTileAsync(
            world.Id, 2, 2, "wood", now, new ResourceSpawnOptions(InitialQuantity: 5m));
        Assert.Equal(spawned.Id, tile.ResourceDepositId);
        Assert.False(spawned.IsDiscovered);

        Assert.True(await service.DiscoverAsync(spawned.Id, actor.Id, now.AddHours(1)));
        var extraction = await service.CollectForActorAsync(spawned.Id, actor.Id, 3, now.AddHours(1));

        Assert.Equal(3m, extraction.Quantity);
        Assert.Equal(3, actor.InventoryQuantity("timber"));
        Assert.Equal(3m, repository.Deposits.Single().Quantity);
        var available = await service.ListAvailableInRegionAsync(
            world.Id, tile.Position, now.AddHours(1), ["WOOD"]);
        Assert.Equal(spawned.Id, Assert.Single(available).Id);
    }

    [Fact]
    public async Task Rejects_resource_in_incompatible_tile_habitat()
    {
        var world = World.Create("Habitat", 8, 8);
        var tile = world.CreateTile(world.PositionAt(1, 1), "forest", Definitions, 0, 20m, 0.5m);
        var service = new NaturalResourceService(
            new FakeNaturalResourceRepository(world, [tile], []), Definitions);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.SpawnOnTileAsync(
            world.Id, 1, 1, "stone", DateTimeOffset.UnixEpoch));

        Assert.Null(tile.ResourceDepositId);
    }

    [Fact]
    public async Task World_event_can_spawn_a_region_resource_for_future_consumers()
    {
        var world = World.Create("Emergence", 64, 64);
        var sourceEventId = Guid.NewGuid();
        var repository = new FakeNaturalResourceRepository(world, [], []);
        var service = new NaturalResourceService(repository, Definitions);

        var spawned = await service.SpawnInRegionAsync(
            world.Id,
            new ChunkCoordinate(1, 1),
            "wood",
            DateTimeOffset.UnixEpoch,
            new ResourceSpawnOptions(SourceWorldEventId: sourceEventId));

        Assert.Equal(ResourceDepositScope.Region, spawned.Scope);
        Assert.Equal(sourceEventId, spawned.SourceWorldEventId);
        Assert.Null(spawned.TileId);
    }

    [Fact]
    public async Task Emergence_event_handler_spawns_resource_with_event_as_auditable_origin()
    {
        var world = World.Create("World event", 64, 64);
        var repository = new FakeNaturalResourceRepository(world, [], []);
        var handler = new NaturalResourceEmergenceHandler(
            new NaturalResourceService(repository, Definitions));
        var worldEvent = new NaturalResourceEmergenceEvent(
            world.Id,
            "wood",
            ResourceDepositScope.Region,
            1,
            1,
            DateTimeOffset.UnixEpoch,
            initialQuantity: 12m,
            capacity: 20m);

        await handler.HandleAsync(worldEvent);

        var spawned = Assert.Single(repository.Deposits);
        Assert.Equal(worldEvent.EventId, spawned.SourceWorldEventId);
        Assert.Equal(12m, spawned.Quantity);
        Assert.Equal(new ChunkCoordinate(1, 1), spawned.Region);
    }

    private sealed class FakeNaturalResourceRepository(
        World world,
        Tile[] tiles,
        Actor[] actors) : INaturalResourceRepository
    {
        public List<ResourceDeposit> Deposits { get; } = [];
        public Task<World?> GetWorldAsync(Guid worldId, CancellationToken cancellationToken = default) =>
            Task.FromResult<World?>(worldId == world.Id ? world : null);
        public Task<Tile?> GetTileAsync(Position position, CancellationToken cancellationToken = default) =>
            Task.FromResult(tiles.SingleOrDefault(tile => tile.Position == position));
        public Task<Actor?> GetActorAsync(Guid actorId, CancellationToken cancellationToken = default) =>
            Task.FromResult(actors.SingleOrDefault(actor => actor.Id == actorId));
        public Task<ResourceDeposit?> GetDepositAsync(Guid depositId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Deposits.SingleOrDefault(deposit => deposit.Id == depositId));
        public Task<IReadOnlyList<ResourceDeposit>> ListAvailableInRegionAsync(
            Guid worldId,
            ChunkCoordinate region,
            IReadOnlyCollection<string>? resourceCodes = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ResourceDeposit>>(Deposits.Where(deposit =>
                deposit.WorldId == worldId && deposit.Region == region && deposit.IsDiscovered &&
                (!deposit.IsExhausted || deposit.IsRenewable) &&
                (resourceCodes is null || resourceCodes.Contains(deposit.ResourceCode))).ToArray());
        public Task<IReadOnlyList<ResourceDeposit>> ListRegeneratingAsync(
            Guid worldId,
            DateTimeOffset worldInstant,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ResourceDeposit>>(Deposits.Where(deposit =>
                deposit.WorldId == worldId && deposit.IsRenewable && deposit.Quantity < deposit.Capacity &&
                deposit.LastRegeneratedAtUtc < worldInstant).ToArray());
        public void Add(ResourceDeposit deposit) => Deposits.Add(deposit);
        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}

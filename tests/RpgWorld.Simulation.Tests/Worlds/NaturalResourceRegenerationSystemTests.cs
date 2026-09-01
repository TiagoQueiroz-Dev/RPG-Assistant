using RpgWorld.Application.Worlds.Resources;
using RpgWorld.Application.Worlds;
using RpgWorld.Domain.Actors;
using RpgWorld.Domain.Worlds;
using RpgWorld.Domain.Worlds.Definitions;
using RpgWorld.Domain.Worlds.Resources;
using RpgWorld.Simulation.Engine;
using RpgWorld.Simulation.Time;
using RpgWorld.Simulation.Worlds.Resources;

namespace RpgWorld.Simulation.Tests.Worlds;

public sealed class NaturalResourceRegenerationSystemTests
{
    [Fact]
    public async Task Regenerates_depleted_renewable_resources_on_world_clock()
    {
        var now = DateTimeOffset.UnixEpoch;
        var definitions = new WorldDefinitionCatalog(
            [new TerrainDefinition("woodland", "Woodland", 1m, true, false, ["wood"])],
            [new BiomeDefinition("forest", "Forest", "woodland", -10m, 40m, 0m, 1m)],
            [new ResourceDefinition("wood", "Wood", "wood", 10m, 2m, ["wood"])]);
        var world = World.Create("Regeneration", 8, 8);
        var tile = world.CreateTile(world.PositionAt(1, 1), "forest", definitions, 0, 20m, 0.5m);
        var deposit = ResourceDeposit.SpawnOnTile(
            world, tile, definitions.ResolveResource("wood"), now, initialQuantity: 0m);
        var repository = new FakeNaturalResourceRepository(world, tile, deposit);
        var system = new NaturalResourceRegenerationSystem(repository);
        var instant = now.AddHours(3);
        var context = new SimulationTickContext(
            world.Id,
            new WorldClockSnapshot(world.Id, instant, TimeSpan.FromHours(1), 1m, instant));

        await system.ExecuteAsync(context);

        Assert.Equal(6m, deposit.Quantity);
        Assert.Equal(1, repository.SaveCount);
    }

    [Fact]
    public async Task Resource_scarcity_reduces_real_regeneration_rate()
    {
        var now = DateTimeOffset.UnixEpoch;
        var definitions = new WorldDefinitionCatalog(
            [new TerrainDefinition("woodland", "Woodland", 1m, true, false)],
            [new BiomeDefinition("forest", "Forest", "woodland", -10m, 40m, 0m, 1m)],
            [new ResourceDefinition("wood", "Wood", "wood", 10m, 2m)]);
        var world = World.Create("Scarce", 8, 8);
        var tile = world.CreateTile(world.PositionAt(1, 1), "forest", definitions, 0, 20m, 0.5m);
        var deposit = ResourceDeposit.SpawnOnTile(world, tile, definitions.ResolveResource("wood"), now, initialQuantity: 0m);
        var system = new NaturalResourceRegenerationSystem(
            new FakeNaturalResourceRepository(world, tile, deposit), new FixedSettingsProvider(resourceScarcity: 2m));
        var instant = now.AddHours(3);

        await system.ExecuteAsync(new SimulationTickContext(world.Id,
            new WorldClockSnapshot(world.Id, instant, TimeSpan.FromHours(1), 1m, instant)));

        Assert.Equal(3m, deposit.Quantity);
    }

    private sealed class FakeNaturalResourceRepository(
        World world,
        Tile tile,
        ResourceDeposit deposit) : INaturalResourceRepository
    {
        public int SaveCount { get; private set; }
        public Task<World?> GetWorldAsync(Guid worldId, CancellationToken cancellationToken = default) => Task.FromResult<World?>(world);
        public Task<Tile?> GetTileAsync(Position position, CancellationToken cancellationToken = default) => Task.FromResult<Tile?>(tile);
        public Task<Actor?> GetActorAsync(Guid actorId, CancellationToken cancellationToken = default) => Task.FromResult<Actor?>(null);
        public Task<ResourceDeposit?> GetDepositAsync(Guid depositId, CancellationToken cancellationToken = default) => Task.FromResult<ResourceDeposit?>(deposit);
        public Task<IReadOnlyList<ResourceDeposit>> ListAvailableInRegionAsync(Guid worldId, ChunkCoordinate region, IReadOnlyCollection<string>? resourceCodes = null, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<ResourceDeposit>>([deposit]);
        public Task<IReadOnlyList<ResourceDeposit>> ListRegeneratingAsync(Guid worldId, DateTimeOffset worldInstant, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ResourceDeposit>>(deposit.WorldId == worldId && deposit.IsRenewable && deposit.Quantity < deposit.Capacity ? [deposit] : []);
        public void Add(ResourceDeposit value) { }
        public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            SaveCount++;
            return Task.CompletedTask;
        }
    }

    private sealed class FixedSettingsProvider(decimal resourceScarcity) : ICampaignSimulationSettingsProvider
    {
        public Task<CampaignSimulationSettingsView> GetEffectiveAsync(
            Guid worldId, CancellationToken cancellationToken = default) => Task.FromResult(
            new CampaignSimulationSettingsView(worldId, 1m, 1m, 1m, 1m, resourceScarcity,
                1m, 1m, 1m, 1, DateTimeOffset.UnixEpoch));
    }
}

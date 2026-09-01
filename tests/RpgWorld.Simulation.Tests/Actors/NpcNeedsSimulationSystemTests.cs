using RpgWorld.Application.Actors;
using RpgWorld.Application.Worlds;
using RpgWorld.Domain.Actors;
using RpgWorld.Domain.Worlds;
using RpgWorld.Simulation.Actors;
using RpgWorld.Simulation.Engine;
using RpgWorld.Simulation.Time;

namespace RpgWorld.Simulation.Tests.Actors;

public sealed class NpcNeedsSimulationSystemTests
{
    [Fact]
    public async Task System_advances_all_npcs_across_several_world_hours_once()
    {
        var start = new DateTimeOffset(2026, 8, 31, 6, 0, 0, TimeSpan.Zero);
        var world = World.Create("Simulated needs", 8, 8);
        var first = NpcActor.Create("First", world, world.PositionAt(1, 1), start);
        var second = NpcActor.Create("Second", world, world.PositionAt(2, 2), start);
        var repository = new FakeNpcNeedsRepository(first, second);
        var system = new NpcNeedsSimulationSystem(repository);
        var instant = start.AddHours(12);
        var context = new SimulationTickContext(
            world.Id,
            new WorldClockSnapshot(world.Id, instant, TimeSpan.FromSeconds(1), 1m, instant));

        await system.ExecuteAsync(context);
        await system.ExecuteAsync(context);

        Assert.All(repository.Npcs, npc =>
        {
            Assert.Equal(48m, npc.Hunger);
            Assert.Equal(64m, npc.Energy);
            Assert.Equal(instant, npc.NeedsUpdatedAt);
        });
        Assert.Equal(1, repository.SaveCalls);
        Assert.Equal("NpcNeeds", system.Name);
        Assert.Equal(20, system.Order);
        Assert.Equal(SimulationSystemFrequencies.Economy, system.Frequency);
    }

    [Fact]
    public async Task Npc_density_limits_detailed_population_processed_per_cycle()
    {
        var start = DateTimeOffset.UnixEpoch;
        var world = World.Create("Sparse", 8, 8);
        var first = NpcActor.Create("First", world, world.PositionAt(1, 1), start);
        var second = NpcActor.Create("Second", world, world.PositionAt(2, 2), start);
        var repository = new FakeNpcNeedsRepository(first, second);
        var system = new NpcNeedsSimulationSystem(repository, new FixedSettingsProvider(0.5m));
        var instant = start.AddHours(1);

        await system.ExecuteAsync(new SimulationTickContext(world.Id,
            new WorldClockSnapshot(world.Id, instant, TimeSpan.FromHours(1), 1m, instant)));

        Assert.Equal(instant, first.NeedsUpdatedAt);
        Assert.Equal(start, second.NeedsUpdatedAt);
    }

    private sealed class FakeNpcNeedsRepository(params NpcActor[] npcs) : INpcNeedsRepository
    {
        public IReadOnlyList<NpcActor> Npcs { get; } = npcs;
        public int SaveCalls { get; private set; }

        public Task<IReadOnlyList<NpcActor>> ListForUpdateAsync(
            Guid worldId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<NpcActor>>(Npcs.Where(npc => npc.WorldId == worldId).ToArray());

        public Task<IReadOnlyList<NpcNeedsSnapshot>> ListUrgentAsync(
            Guid worldId,
            decimal minimumHunger,
            decimal maximumEnergy,
            int limit = 100,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            SaveCalls++;
            return Task.CompletedTask;
        }
    }

    private sealed class FixedSettingsProvider(decimal npcDensity) : ICampaignSimulationSettingsProvider
    {
        public Task<CampaignSimulationSettingsView> GetEffectiveAsync(
            Guid worldId, CancellationToken cancellationToken = default) => Task.FromResult(
            new CampaignSimulationSettingsView(worldId, npcDensity, 1m, 1m, 1m, 1m,
                1m, 1m, 1m, 1, DateTimeOffset.UnixEpoch));
    }
}

using RpgWorld.Application.Realtime;
using RpgWorld.Application.Worlds;
using RpgWorld.Domain.Worlds;
using RpgWorld.Simulation.Engine;
using RpgWorld.Simulation.Time;

namespace RpgWorld.Simulation.Tests.Time;

public sealed class WorldTimeCommandServiceTests
{
    [Fact]
    public async Task Pause_resume_and_speed_are_persisted_and_published_to_game_master()
    {
        var fixture = new Fixture();

        var paused = await fixture.Service.PauseAsync(fixture.World.Id);
        var resumed = await fixture.Service.ResumeAsync(fixture.World.Id);
        var accelerated = await fixture.Service.SetMultiplierAsync(fixture.World.Id, 4m);

        Assert.False(paused.IsRunning);
        Assert.True(resumed.IsRunning);
        Assert.Equal(4m, accelerated.RealTimeMultiplier);
        Assert.Equal(1, fixture.Clock.RebaseCalls);
        Assert.Equal(2, fixture.Repository.SaveCalls);
        Assert.Equal(
            ["world.time.paused", "world.time.resumed", "world.time.speed-changed"],
            fixture.Publisher.Messages.Select(message => message.UpdateType));
    }

    [Fact]
    public async Task Manual_advance_moves_clock_scheduler_and_runs_due_systems_even_while_paused()
    {
        var fixture = new Fixture();
        await fixture.Service.PauseAsync(fixture.World.Id);
        var before = fixture.Clock.CurrentInstant;

        var result = await fixture.Service.AdvanceAsync(fixture.World.Id, TimeSpan.FromHours(6));

        Assert.False(result.IsRunning);
        Assert.Equal(before.AddHours(6), result.CurrentInstant);
        Assert.Equal(TimeSpan.FromHours(6), fixture.Scheduler.AdvancedBy);
        Assert.Equal(result.CurrentInstant, Assert.Single(fixture.Runner.Contexts).Clock.CurrentInstant);
        Assert.Equal("world.time.advanced", fixture.Publisher.Messages[^1].UpdateType);
    }

    [Fact]
    public async Task Gate_serializes_incompatible_commands_for_same_world()
    {
        using var gate = new WorldCommandGate();
        var worldId = Guid.NewGuid();
        var firstEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseFirst = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondEntered = false;
        var first = gate.ExecuteAsync(worldId, async _ =>
        {
            firstEntered.SetResult();
            await releaseFirst.Task;
        });
        await firstEntered.Task;
        var second = gate.ExecuteAsync(worldId, _ =>
        {
            secondEntered = true;
            return Task.CompletedTask;
        });

        await Task.Delay(50);
        Assert.False(secondEntered);
        releaseFirst.SetResult();
        await Task.WhenAll(first, second);
        Assert.True(secondEntered);
    }

    [Fact]
    public void Scheduler_virtual_advance_makes_slow_system_due_without_real_time_passing()
    {
        var scheduler = new SimulationScheduler(new SimulationEngineOptions());
        var system = new SlowSystem();
        var worldId = Guid.NewGuid();
        var now = DateTimeOffset.UnixEpoch;
        Assert.True(scheduler.TryBegin(worldId, system, now, out var first));
        scheduler.Complete(first!, now, TimeSpan.Zero, true);
        Assert.False(scheduler.TryBegin(worldId, system, now, out _));

        scheduler.AdvanceWorld(worldId, TimeSpan.FromHours(1));

        Assert.True(scheduler.TryBegin(worldId, system, now, out var advanced));
        Assert.Equal(now.AddHours(1), advanced!.StartedAtUtc);
    }

    private sealed class Fixture
    {
        public Fixture()
        {
            World = World.Create("Controlled world", 8, 8);
            Repository = new FakeWorldRepository(World);
            Clock = new FakeClockService(World.Id);
            Scheduler = new RecordingScheduler();
            Runner = new RecordingRunner();
            Publisher = new RecordingPublisher();
            Service = new WorldTimeCommandService(
                Repository,
                Clock,
                new WorldCommandGate(),
                Scheduler,
                Runner,
                Publisher,
                new FixedTimeProvider());
        }

        public World World { get; }
        public FakeWorldRepository Repository { get; }
        public FakeClockService Clock { get; }
        public RecordingScheduler Scheduler { get; }
        public RecordingRunner Runner { get; }
        public RecordingPublisher Publisher { get; }
        public WorldTimeCommandService Service { get; }
    }

    private sealed class FakeWorldRepository(World world) : IWorldSimulationRepository
    {
        public int SaveCalls { get; private set; }
        public Task<IReadOnlyList<Guid>> ListRunningWorldIdsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Guid>>(world.IsSimulationRunning ? [world.Id] : []);
        public Task<World?> GetAsync(Guid worldId, CancellationToken cancellationToken = default) =>
            Task.FromResult<World?>(worldId == world.Id ? world : null);
        public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            SaveCalls++;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeClockService(Guid worldId) : IWorldClockService
    {
        public int RebaseCalls { get; private set; }
        public DateTimeOffset CurrentInstant { get; private set; } = DateTimeOffset.UnixEpoch;
        private TimeSpan TickDuration { get; set; } = TimeSpan.FromMinutes(1);
        private decimal Multiplier { get; set; } = 1m;
        public Task<WorldClockSnapshot> GetAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(Snapshot());
        public Task<WorldClockSnapshot> AdvanceTicksAsync(Guid id, int tickCount = 1, CancellationToken cancellationToken = default)
        { CurrentInstant = CurrentInstant.Add(TickDuration * tickCount); return Task.FromResult(Snapshot()); }
        public Task<WorldClockSnapshot> AdvanceByAsync(Guid id, TimeSpan duration, CancellationToken cancellationToken = default)
        { CurrentInstant = CurrentInstant.Add(duration); return Task.FromResult(Snapshot()); }
        public Task<WorldClockSnapshot> SynchronizeAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(Snapshot());
        public Task<WorldClockSnapshot> RebaseAsync(Guid id, CancellationToken cancellationToken = default)
        { RebaseCalls++; return Task.FromResult(Snapshot()); }
        public Task<WorldClockSnapshot> ConfigureAsync(Guid id, TimeSpan tickDuration, decimal realTimeMultiplier, CancellationToken cancellationToken = default)
        { TickDuration = tickDuration; Multiplier = realTimeMultiplier; return Task.FromResult(Snapshot()); }
        private WorldClockSnapshot Snapshot() => new(worldId, CurrentInstant, TickDuration, Multiplier, DateTimeOffset.UnixEpoch);
    }

    private sealed class RecordingScheduler : ISimulationScheduler
    {
        public TimeSpan AdvancedBy { get; private set; }
        public void AdvanceWorld(Guid worldId, TimeSpan duration) => AdvancedBy += duration;
        public bool TryBegin(Guid worldId, ISimulationSystem system, DateTimeOffset observedAtUtc, out SimulationSystemExecution? execution)
        { execution = null; return false; }
        public void Complete(SimulationSystemExecution execution, DateTimeOffset completedAtUtc, TimeSpan duration, bool succeeded) { }
        public IReadOnlyList<SimulationSystemDiagnostic> GetDiagnostics(Guid? worldId = null) => [];
    }

    private sealed class RecordingRunner : ISimulationSystemRunner
    {
        public List<SimulationTickContext> Contexts { get; } = [];
        public Task RunAsync(SimulationTickContext context, CancellationToken cancellationToken = default)
        { Contexts.Add(context); return Task.CompletedTask; }
    }

    private sealed class RecordingPublisher : IWorldUpdatePublisher
    {
        public List<WorldUpdateMessage> Messages { get; } = [];
        public Task PublishToGameMasterAsync(WorldUpdateMessage message, CancellationToken cancellationToken = default)
        { Messages.Add(message); return Task.CompletedTask; }
        public Task PublishToWorldAsync(WorldUpdateMessage message, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task PublishToChunkAsync(Guid chunkId, WorldUpdateMessage message, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task PublishToPlayerAsync(Guid playerId, WorldUpdateMessage message, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class SlowSystem : ISimulationSystem
    {
        public string Name => "slow";
        public int Order => 0;
        public TimeSpan Frequency => TimeSpan.FromHours(1);
        public Task ExecuteAsync(SimulationTickContext context, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(2026, 8, 30, 12, 0, 0, TimeSpan.Zero);
    }
}

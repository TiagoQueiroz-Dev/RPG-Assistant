using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RpgWorld.Application.Worlds;
using RpgWorld.Domain.Worlds;
using RpgWorld.Simulation.Engine;
using RpgWorld.Simulation.Time;
using RpgWorld.Simulation;

namespace RpgWorld.Simulation.Tests.Engine;

public sealed class SimulationEngineTests
{
    [Fact]
    public void Simulation_registration_adds_engine_as_hosted_service()
    {
        var services = new ServiceCollection();

        services.AddSimulation();

        Assert.Contains(services, descriptor =>
            descriptor.ServiceType == typeof(IHostedService) &&
            descriptor.ImplementationType == typeof(SimulationEngine));
    }

    [Fact]
    public async Task Cycle_ticks_only_running_worlds_and_executes_systems_in_order()
    {
        var firstWorld = Guid.NewGuid();
        var secondWorld = Guid.NewGuid();
        var calls = new List<string>();
        var clock = new RecordingClockService();
        await using var provider = CreateProvider(
            new FakeWorldSimulationRepository([firstWorld, secondWorld]),
            clock,
            new RecordingSystem("later", order: 20, calls),
            new RecordingSystem("earlier", order: 10, calls));
        var engine = CreateEngine(provider);

        await engine.RunCycleAsync();

        Assert.Equal([firstWorld, secondWorld], clock.TickedWorldIds);
        Assert.Equal(
            [
                $"earlier:{firstWorld}",
                $"later:{firstWorld}",
                $"earlier:{secondWorld}",
                $"later:{secondWorld}"
            ],
            calls);
    }

    [Fact]
    public async Task Engine_respects_different_system_frequencies()
    {
        var worldId = Guid.NewGuid();
        var calls = new List<string>();
        var time = new ManualTimeProvider(DateTimeOffset.UnixEpoch);
        await using var provider = CreateProvider(
            new FakeWorldSimulationRepository([worldId]),
            new RecordingClockService(),
            new RecordingSystem("fast", 10, calls, TimeSpan.FromMilliseconds(100)),
            new RecordingSystem("slow", 20, calls, TimeSpan.FromSeconds(1)));
        var engine = CreateEngine(provider, timeProvider: time);

        await engine.RunCycleAsync();
        time.Advance(TimeSpan.FromMilliseconds(99));
        await engine.RunCycleAsync();
        time.Advance(TimeSpan.FromMilliseconds(1));
        await engine.RunCycleAsync();
        time.Advance(TimeSpan.FromMilliseconds(900));
        await engine.RunCycleAsync();

        Assert.Equal(3, calls.Count(call => call.StartsWith("fast", StringComparison.Ordinal)));
        Assert.Equal(2, calls.Count(call => call.StartsWith("slow", StringComparison.Ordinal)));
    }

    [Fact]
    public void Scheduler_avoids_drift_and_reports_execution_diagnostics()
    {
        var options = new SimulationEngineOptions();
        var scheduler = new SimulationScheduler(options);
        var system = new RecordingSystem(
            "movement",
            0,
            [],
            SimulationSystemFrequencies.Movement);
        var worldId = Guid.NewGuid();
        var initial = DateTimeOffset.UnixEpoch;
        Assert.True(scheduler.TryBegin(worldId, system, initial, out var first));
        scheduler.Complete(first!, initial.AddMilliseconds(12), TimeSpan.FromMilliseconds(12), true);

        Assert.True(scheduler.TryBegin(
            worldId,
            system,
            initial.AddMilliseconds(350),
            out var delayed));
        scheduler.Complete(
            delayed!,
            initial.AddMilliseconds(370),
            TimeSpan.FromMilliseconds(20),
            false);

        var diagnostic = Assert.Single(scheduler.GetDiagnostics(worldId));
        Assert.Equal(initial.AddMilliseconds(100), delayed!.ScheduledForUtc);
        Assert.Equal(initial.AddMilliseconds(400), diagnostic.NextExecutionAtUtc);
        Assert.Equal(TimeSpan.FromMilliseconds(20), diagnostic.LastDuration);
        Assert.Equal(2, diagnostic.ExecutionCount);
        Assert.Equal(1, diagnostic.FailureCount);
    }

    [Fact]
    public void Scheduler_applies_frequency_override_without_engine_changes()
    {
        var scheduler = new SimulationScheduler(new SimulationEngineOptions
        {
            SystemFrequencyOverrides = new Dictionary<string, TimeSpan>
            {
                ["movement"] = TimeSpan.FromMilliseconds(250)
            }
        });
        var system = new RecordingSystem(
            "Movement",
            0,
            [],
            SimulationSystemFrequencies.Movement);
        var initial = DateTimeOffset.UnixEpoch;

        Assert.True(scheduler.TryBegin(Guid.NewGuid(), system, initial, out var execution));

        Assert.Equal(TimeSpan.FromMilliseconds(250), execution!.Frequency);
    }

    [Fact]
    public async Task Failed_system_is_logged_without_preventing_remaining_systems()
    {
        var worldId = Guid.NewGuid();
        var calls = new List<string>();
        var logger = new RecordingLogger<SimulationEngine>();
        await using var provider = CreateProvider(
            new FakeWorldSimulationRepository([worldId]),
            new RecordingClockService(),
            new ThrowingSystem(),
            new RecordingSystem("healthy", order: 20, calls));
        var engine = CreateEngine(provider, logger);

        await engine.RunCycleAsync();

        Assert.Equal([$"healthy:{worldId}"], calls);
        Assert.Contains(logger.Entries, entry =>
            entry.Level == LogLevel.Error &&
            entry.Message.Contains("broken", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Hosted_engine_starts_without_clients_and_stops_gracefully()
    {
        var worldId = Guid.NewGuid();
        var blockingSystem = new BlockingSystem();
        await using var provider = CreateProvider(
            new FakeWorldSimulationRepository([worldId]),
            new RecordingClockService(),
            blockingSystem);
        var engine = CreateEngine(provider);

        await engine.StartAsync(CancellationToken.None);
        await blockingSystem.Started.Task.WaitAsync(TimeSpan.FromSeconds(2));
        using var shutdownTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        await engine.StopAsync(shutdownTimeout.Token);

        Assert.True(blockingSystem.CancellationObserved);
    }

    private static ServiceProvider CreateProvider(
        IWorldSimulationRepository repository,
        IWorldClockService clock,
        params ISimulationSystem[] systems)
    {
        var services = new ServiceCollection();
        services.AddSingleton(repository);
        services.AddSingleton(clock);

        foreach (var system in systems)
        {
            services.AddSingleton(typeof(ISimulationSystem), system);
        }

        var options = new SimulationEngineOptions
        {
            TickInterval = TimeSpan.FromMilliseconds(10)
        };
        services.AddSingleton(options);
        services.AddSingleton<ISimulationScheduler, SimulationScheduler>();

        return services.BuildServiceProvider(validateScopes: true);
    }

    private static SimulationEngine CreateEngine(
        ServiceProvider provider,
        ILogger<SimulationEngine>? logger = null,
        TimeProvider? timeProvider = null) =>
        new(
            provider.GetRequiredService<IServiceScopeFactory>(),
            provider.GetRequiredService<SimulationEngineOptions>(),
            timeProvider ?? TimeProvider.System,
            provider.GetRequiredService<ISimulationScheduler>(),
            logger ?? new RecordingLogger<SimulationEngine>());

    private sealed class FakeWorldSimulationRepository(IReadOnlyList<Guid> runningWorldIds)
        : IWorldSimulationRepository
    {
        public Task<IReadOnlyList<Guid>> ListRunningWorldIdsAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult(runningWorldIds);

        public Task<World?> GetAsync(
            Guid worldId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<World?>(null);

        public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class RecordingClockService : IWorldClockService
    {
        private readonly List<Guid> _tickedWorldIds = [];

        public IReadOnlyList<Guid> TickedWorldIds => _tickedWorldIds;

        public Task<WorldClockSnapshot> AdvanceTicksAsync(
            Guid worldId,
            int tickCount = 1,
            CancellationToken cancellationToken = default)
        {
            _tickedWorldIds.Add(worldId);
            return Task.FromResult(Snapshot(worldId));
        }

        public Task<WorldClockSnapshot> GetAsync(
            Guid worldId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Snapshot(worldId));

        public Task<WorldClockSnapshot> SynchronizeAsync(
            Guid worldId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Snapshot(worldId));

        public Task<WorldClockSnapshot> ConfigureAsync(
            Guid worldId,
            TimeSpan tickDuration,
            decimal realTimeMultiplier,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Snapshot(worldId));

        private static WorldClockSnapshot Snapshot(Guid worldId) =>
            new(
                worldId,
                DateTimeOffset.UnixEpoch,
                TimeSpan.FromMinutes(1),
                1m,
                DateTimeOffset.UnixEpoch);
    }

    private sealed class RecordingSystem(
        string name,
        int order,
        ICollection<string> calls,
        TimeSpan? frequency = null) : ISimulationSystem
    {
        public string Name => name;

        public int Order => order;

        public TimeSpan Frequency => frequency ?? TimeSpan.FromMilliseconds(10);

        public Task ExecuteAsync(
            SimulationTickContext context,
            CancellationToken cancellationToken = default)
        {
            calls.Add($"{Name}:{context.WorldId}");
            return Task.CompletedTask;
        }
    }

    private sealed class ThrowingSystem : ISimulationSystem
    {
        public string Name => "broken";

        public int Order => 10;

        public TimeSpan Frequency => TimeSpan.FromMilliseconds(10);

        public Task ExecuteAsync(
            SimulationTickContext context,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Expected test failure.");
    }

    private sealed class BlockingSystem : ISimulationSystem
    {
        public TaskCompletionSource Started { get; } = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        public bool CancellationObserved { get; private set; }

        public string Name => "blocking";

        public int Order => 0;

        public TimeSpan Frequency => TimeSpan.FromMilliseconds(10);

        public async Task ExecuteAsync(
            SimulationTickContext context,
            CancellationToken cancellationToken = default)
        {
            Started.TrySetResult();

            try
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                CancellationObserved = true;
                throw;
            }
        }
    }

    private sealed class ManualTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        private DateTimeOffset _utcNow = utcNow;
        private long _timestamp;

        public override long TimestampFrequency => TimeSpan.TicksPerSecond;

        public override DateTimeOffset GetUtcNow() => _utcNow;

        public override long GetTimestamp() => _timestamp;

        public void Advance(TimeSpan duration)
        {
            _utcNow = _utcNow.Add(duration);
            _timestamp += duration.Ticks;
        }
    }

    private sealed class RecordingLogger<T> : ILogger<T>
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            Entries.Add((logLevel, formatter(state, exception)));
    }
}

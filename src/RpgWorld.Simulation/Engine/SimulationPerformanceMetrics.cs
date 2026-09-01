using System.Collections.Concurrent;
using System.Diagnostics.Metrics;

namespace RpgWorld.Simulation.Engine;

public sealed class SimulationPerformanceMetrics : ISimulationPerformanceMetrics, IDisposable
{
    public const string MeterName = "RpgWorld.Simulation";
    private readonly Meter _meter = new(MeterName);
    private readonly Counter<long> _ticks;
    private readonly Counter<long> _lateTicks;
    private readonly Counter<long> _actors;
    private readonly Histogram<double> _tickDuration;
    private readonly Histogram<double> _systemDuration;
    private readonly Histogram<int> _worlds;
    private readonly Histogram<int> _activeChunks;
    private readonly ConcurrentDictionary<Guid, TickState> _tickStates = [];
    private readonly ConcurrentDictionary<(Guid WorldId, string System), SystemState> _systemStates = [];
    private readonly Lock _cycleLock = new();
    private long _cycleCount;
    private int _lastWorldCount;
    private double _lastCycleDurationMilliseconds;

    public SimulationPerformanceMetrics()
    {
        _ticks = _meter.CreateCounter<long>("rpgworld.simulation.ticks");
        _lateTicks = _meter.CreateCounter<long>("rpgworld.simulation.ticks.late");
        _actors = _meter.CreateCounter<long>("rpgworld.simulation.actors.processed");
        _tickDuration = _meter.CreateHistogram<double>("rpgworld.simulation.tick.duration", "ms");
        _systemDuration = _meter.CreateHistogram<double>("rpgworld.simulation.system.duration", "ms");
        _worlds = _meter.CreateHistogram<int>("rpgworld.simulation.worlds", description: "Running worlds per cycle");
        _activeChunks = _meter.CreateHistogram<int>("rpgworld.simulation.chunks.active");
    }

    public void RecordCycle(int worldCount, TimeSpan duration)
    {
        if (worldCount < 0) throw new ArgumentOutOfRangeException(nameof(worldCount));
        if (duration < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(duration));
        lock (_cycleLock)
        {
            _cycleCount++;
            _lastWorldCount = worldCount;
            _lastCycleDurationMilliseconds = duration.TotalMilliseconds;
        }
        _worlds.Record(worldCount);
    }

    public void RecordTick(Guid worldId, TimeSpan duration, SimulationTickWorkload workload, TimeSpan budget)
    {
        ArgumentNullException.ThrowIfNull(workload);
        if (worldId == Guid.Empty) throw new ArgumentException("World identifier is required.", nameof(worldId));
        if (duration < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(duration));
        if (budget <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(budget));
        var late = duration > budget;
        _tickStates.GetOrAdd(worldId, static _ => new TickState()).Record(duration, workload, late);
        _ticks.Add(1);
        if (late) _lateTicks.Add(1);
        _actors.Add(workload.ActorsProcessed);
        _tickDuration.Record(duration.TotalMilliseconds);
        _activeChunks.Record(workload.ActiveChunks);
    }

    public void RecordSystem(Guid worldId, string systemName, TimeSpan duration, long actorsProcessed,
        TimeSpan budget, bool succeeded)
    {
        if (string.IsNullOrWhiteSpace(systemName)) throw new ArgumentException("System name is required.", nameof(systemName));
        if (worldId == Guid.Empty) throw new ArgumentException("World identifier is required.", nameof(worldId));
        if (duration < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(duration));
        if (actorsProcessed < 0) throw new ArgumentOutOfRangeException(nameof(actorsProcessed));
        if (budget <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(budget));
        _systemStates.GetOrAdd((worldId, systemName), static _ => new SystemState())
            .Record(duration, actorsProcessed, duration > budget, succeeded);
        _systemDuration.Record(duration.TotalMilliseconds, new KeyValuePair<string, object?>("system", systemName));
    }

    public SimulationPerformanceSnapshot GetSnapshot(Guid? worldId = null)
    {
        long cycleCount;
        int lastWorldCount;
        double lastCycleDuration;
        lock (_cycleLock)
        {
            cycleCount = _cycleCount;
            lastWorldCount = _lastWorldCount;
            lastCycleDuration = _lastCycleDurationMilliseconds;
        }

        var ticks = _tickStates.Where(pair => worldId is null || pair.Key == worldId)
            .Select(pair => pair.Value.Snapshot()).ToArray();
        var systems = _systemStates.Where(pair => worldId is null || pair.Key.WorldId == worldId)
            .GroupBy(pair => pair.Key.System, StringComparer.Ordinal)
            .Select(group => SystemState.Combine(group.Key, group.Select(pair => pair.Value.Snapshot())))
            .OrderBy(value => value.SystemName, StringComparer.Ordinal).ToArray();
        var tickCount = ticks.Sum(value => value.TickCount);
        return new SimulationPerformanceSnapshot(worldId, cycleCount, lastWorldCount, lastCycleDuration,
            tickCount, ticks.Sum(value => value.LateTickCount),
            tickCount == 0 ? 0 : ticks.Sum(value => value.TotalDurationMilliseconds) / tickCount,
            ticks.Select(value => value.MaximumDurationMilliseconds).DefaultIfEmpty().Max(),
            ticks.Sum(value => value.LastActorsProcessed), ticks.Sum(value => value.TotalActorsProcessed),
            ticks.Sum(value => value.LastActiveChunkCount), systems);
    }

    public void Dispose() => _meter.Dispose();

    private sealed class TickState
    {
        private readonly Lock _lock = new();
        private long _count, _late, _lastActors, _totalActors, _totalDurationTicks, _maximumDurationTicks;
        private int _lastChunks;

        public void Record(TimeSpan duration, SimulationTickWorkload workload, bool late)
        {
            lock (_lock)
            {
                _count++;
                if (late) _late++;
                _lastActors = workload.ActorsProcessed;
                _totalActors += workload.ActorsProcessed;
                _lastChunks = workload.ActiveChunks;
                _totalDurationTicks += duration.Ticks;
                _maximumDurationTicks = Math.Max(_maximumDurationTicks, duration.Ticks);
            }
        }

        public TickSnapshot Snapshot()
        {
            lock (_lock) return new(_count, _late, TimeSpan.FromTicks(_totalDurationTicks).TotalMilliseconds,
                TimeSpan.FromTicks(_maximumDurationTicks).TotalMilliseconds, _lastActors, _totalActors, _lastChunks);
        }
    }

    private sealed class SystemState
    {
        private readonly Lock _lock = new();
        private long _count, _failures, _budgetExceeded, _totalDurationTicks, _maximumDurationTicks;
        private long _lastActors, _totalActors;

        public void Record(TimeSpan duration, long actors, bool budgetExceeded, bool succeeded)
        {
            lock (_lock)
            {
                _count++;
                if (!succeeded) _failures++;
                if (budgetExceeded) _budgetExceeded++;
                _totalDurationTicks += duration.Ticks;
                _maximumDurationTicks = Math.Max(_maximumDurationTicks, duration.Ticks);
                _lastActors = actors;
                _totalActors += actors;
            }
        }

        public SystemSnapshot Snapshot()
        {
            lock (_lock) return new(_count, _failures, _budgetExceeded, _totalDurationTicks,
                _maximumDurationTicks, _lastActors, _totalActors);
        }

        public static SimulationSystemPerformanceSnapshot Combine(string name, IEnumerable<SystemSnapshot> values)
        {
            var snapshots = values.ToArray();
            var executions = snapshots.Sum(value => value.ExecutionCount);
            return new(name, executions, snapshots.Sum(value => value.FailureCount),
                snapshots.Sum(value => value.BudgetExceededCount),
                executions == 0 ? 0 : TimeSpan.FromTicks(snapshots.Sum(value => value.TotalDurationTicks)).TotalMilliseconds / executions,
                TimeSpan.FromTicks(snapshots.Select(value => value.MaximumDurationTicks).DefaultIfEmpty().Max()).TotalMilliseconds,
                snapshots.Sum(value => value.LastActorsProcessed), snapshots.Sum(value => value.TotalActorsProcessed));
        }
    }

    private sealed record TickSnapshot(long TickCount, long LateTickCount, double TotalDurationMilliseconds,
        double MaximumDurationMilliseconds, long LastActorsProcessed, long TotalActorsProcessed, int LastActiveChunkCount);
    private sealed record SystemSnapshot(long ExecutionCount, long FailureCount, long BudgetExceededCount,
        long TotalDurationTicks, long MaximumDurationTicks, long LastActorsProcessed, long TotalActorsProcessed);
}

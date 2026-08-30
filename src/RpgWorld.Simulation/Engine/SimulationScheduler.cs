namespace RpgWorld.Simulation.Engine;

public sealed class SimulationScheduler : ISimulationScheduler
{
    private readonly Lock _lock = new();
    private readonly Dictionary<ScheduleKey, ScheduleState> _states = [];
    private readonly IReadOnlyDictionary<string, TimeSpan> _overrides;
    private readonly Dictionary<Guid, TimeSpan> _worldOffsets = [];

    public SimulationScheduler(SimulationEngineOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();
        _overrides = new Dictionary<string, TimeSpan>(
            options.SystemFrequencyOverrides,
            StringComparer.OrdinalIgnoreCase);
    }

    public bool TryBegin(
        Guid worldId,
        ISimulationSystem system,
        DateTimeOffset observedAtUtc,
        out SimulationSystemExecution? execution)
    {
        ArgumentNullException.ThrowIfNull(system);
        var frequency = ResolveFrequency(system);
        var observedUtc = observedAtUtc.ToUniversalTime();
        var key = new ScheduleKey(worldId, system.Name);

        lock (_lock)
        {
            if (_worldOffsets.TryGetValue(worldId, out var offset)) observedUtc = observedUtc.Add(offset);
            if (!_states.TryGetValue(key, out var state))
            {
                state = new ScheduleState(frequency, observedUtc);
                _states.Add(key, state);
            }
            else if (state.Frequency != frequency)
            {
                state.Frequency = frequency;
                state.NextExecutionAtUtc = state.LastStartedAtUtc?.Add(frequency) ?? observedUtc;
            }

            if (observedUtc < state.NextExecutionAtUtc)
            {
                execution = null;
                return false;
            }

            var scheduledFor = state.NextExecutionAtUtc;
            state.NextExecutionAtUtc = NextOccurrence(scheduledFor, frequency, observedUtc);
            state.LastStartedAtUtc = observedUtc;
            execution = new SimulationSystemExecution(
                worldId,
                system.Name,
                scheduledFor,
                observedUtc,
                frequency);
            return true;
        }
    }

    public void AdvanceWorld(Guid worldId, TimeSpan duration)
    {
        if (worldId == Guid.Empty) throw new ArgumentException("World identifier is required.", nameof(worldId));
        if (duration <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(duration));
        lock (_lock)
        {
            _worldOffsets.TryGetValue(worldId, out var current);
            _worldOffsets[worldId] = current.Add(duration);
        }
    }

    public void Complete(
        SimulationSystemExecution execution,
        DateTimeOffset completedAtUtc,
        TimeSpan duration,
        bool succeeded)
    {
        ArgumentNullException.ThrowIfNull(execution);
        if (duration < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(duration));

        lock (_lock)
        {
            var state = _states[new ScheduleKey(execution.WorldId, execution.SystemName)];
            state.LastCompletedAtUtc = completedAtUtc.ToUniversalTime();
            state.LastDuration = duration;
            state.ExecutionCount++;
            if (!succeeded) state.FailureCount++;
        }
    }

    public IReadOnlyList<SimulationSystemDiagnostic> GetDiagnostics(Guid? worldId = null)
    {
        lock (_lock)
        {
            return _states
                .Where(pair => worldId is null || pair.Key.WorldId == worldId)
                .OrderBy(pair => pair.Key.WorldId)
                .ThenBy(pair => pair.Key.SystemName, StringComparer.Ordinal)
                .Select(pair => pair.Value.ToDiagnostic(pair.Key))
                .ToArray();
        }
    }

    private TimeSpan ResolveFrequency(ISimulationSystem system)
    {
        if (string.IsNullOrWhiteSpace(system.Name))
            throw new InvalidOperationException("Simulation system name cannot be empty.");
        var frequency = _overrides.TryGetValue(system.Name, out var configured)
            ? configured
            : system.Frequency;
        return frequency > TimeSpan.Zero
            ? frequency
            : throw new InvalidOperationException(
                $"Simulation system '{system.Name}' must declare a positive frequency.");
    }

    private static DateTimeOffset NextOccurrence(
        DateTimeOffset scheduledFor,
        TimeSpan frequency,
        DateTimeOffset observedAt)
    {
        var elapsedTicks = (observedAt - scheduledFor).Ticks;
        var intervals = (elapsedTicks / frequency.Ticks) + 1;
        return scheduledFor.AddTicks(checked(intervals * frequency.Ticks));
    }

    private readonly record struct ScheduleKey(Guid WorldId, string SystemName);

    private sealed class ScheduleState(TimeSpan frequency, DateTimeOffset nextExecutionAtUtc)
    {
        public TimeSpan Frequency { get; set; } = frequency;
        public DateTimeOffset NextExecutionAtUtc { get; set; } = nextExecutionAtUtc;
        public DateTimeOffset? LastStartedAtUtc { get; set; }
        public DateTimeOffset? LastCompletedAtUtc { get; set; }
        public TimeSpan? LastDuration { get; set; }
        public long ExecutionCount { get; set; }
        public long FailureCount { get; set; }

        public SimulationSystemDiagnostic ToDiagnostic(ScheduleKey key) => new(
            key.WorldId,
            key.SystemName,
            Frequency,
            LastStartedAtUtc,
            LastCompletedAtUtc,
            LastDuration,
            NextExecutionAtUtc,
            ExecutionCount,
            FailureCount);
    }
}

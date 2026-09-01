namespace RpgWorld.Simulation.Engine;

public sealed class SimulationEngineOptions
{
    public static readonly TimeSpan DefaultTickInterval = TimeSpan.FromSeconds(1);
    public static readonly TimeSpan DefaultTickBudget = TimeSpan.FromMilliseconds(750);
    public static readonly TimeSpan DefaultSystemBudget = TimeSpan.FromMilliseconds(100);

    public TimeSpan TickInterval { get; init; } = DefaultTickInterval;
    public TimeSpan TickBudget { get; init; } = DefaultTickBudget;
    public TimeSpan SystemBudget { get; init; } = DefaultSystemBudget;

    public IReadOnlyDictionary<string, TimeSpan> SystemFrequencyOverrides { get; init; } =
        new Dictionary<string, TimeSpan>(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyDictionary<string, TimeSpan> SystemBudgetOverrides { get; init; } =
        new Dictionary<string, TimeSpan>(StringComparer.OrdinalIgnoreCase);

    public TimeSpan GetSystemBudget(string systemName) =>
        SystemBudgetOverrides.FirstOrDefault(entry =>
            string.Equals(entry.Key, systemName, StringComparison.OrdinalIgnoreCase)) is { Key: not null } configured
            ? configured.Value
            : SystemBudget;

    internal void Validate()
    {
        if (TickInterval <= TimeSpan.Zero || TickInterval > TimeSpan.FromHours(1))
        {
            throw new ArgumentOutOfRangeException(
                nameof(TickInterval),
                "Engine tick interval must be between one tick and one hour.");
        }

        if (TickBudget <= TimeSpan.Zero || SystemBudget <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(TickBudget), "Tick and system budgets must be positive.");

        foreach (var (systemName, frequency) in SystemFrequencyOverrides)
        {
            if (string.IsNullOrWhiteSpace(systemName) || frequency <= TimeSpan.Zero)
            {
                throw new ArgumentException(
                    "System frequency overrides require a name and a positive duration.",
                    nameof(SystemFrequencyOverrides));
            }
        }

        foreach (var (systemName, budget) in SystemBudgetOverrides)
        {
            if (string.IsNullOrWhiteSpace(systemName) || budget <= TimeSpan.Zero)
                throw new ArgumentException(
                    "System budget overrides require a name and a positive duration.",
                    nameof(SystemBudgetOverrides));
        }
    }
}

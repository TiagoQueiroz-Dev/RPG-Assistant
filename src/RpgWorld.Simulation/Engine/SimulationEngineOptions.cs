namespace RpgWorld.Simulation.Engine;

public sealed class SimulationEngineOptions
{
    public static readonly TimeSpan DefaultTickInterval = TimeSpan.FromSeconds(1);

    public TimeSpan TickInterval { get; init; } = DefaultTickInterval;

    public IReadOnlyDictionary<string, TimeSpan> SystemFrequencyOverrides { get; init; } =
        new Dictionary<string, TimeSpan>(StringComparer.OrdinalIgnoreCase);

    internal void Validate()
    {
        if (TickInterval <= TimeSpan.Zero || TickInterval > TimeSpan.FromHours(1))
        {
            throw new ArgumentOutOfRangeException(
                nameof(TickInterval),
                "Engine tick interval must be between one tick and one hour.");
        }

        foreach (var (systemName, frequency) in SystemFrequencyOverrides)
        {
            if (string.IsNullOrWhiteSpace(systemName) || frequency <= TimeSpan.Zero)
            {
                throw new ArgumentException(
                    "System frequency overrides require a name and a positive duration.",
                    nameof(SystemFrequencyOverrides));
            }
        }
    }
}

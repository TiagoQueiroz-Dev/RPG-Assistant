namespace RpgWorld.Simulation.Engine;

public sealed class SimulationEngineOptions
{
    public static readonly TimeSpan DefaultTickInterval = TimeSpan.FromSeconds(1);

    public TimeSpan TickInterval { get; init; } = DefaultTickInterval;

    internal void Validate()
    {
        if (TickInterval <= TimeSpan.Zero || TickInterval > TimeSpan.FromHours(1))
        {
            throw new ArgumentOutOfRangeException(
                nameof(TickInterval),
                "Engine tick interval must be between one tick and one hour.");
        }
    }
}

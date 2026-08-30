namespace RpgWorld.Simulation.Regions;

public sealed record SimulationLevelOptions
{
    public SimulationLevelOptions(int detailedRadius = 1, int regionalRadius = 4)
    {
        if (detailedRadius < 0) throw new ArgumentOutOfRangeException(nameof(detailedRadius));
        if (regionalRadius < detailedRadius) throw new ArgumentOutOfRangeException(nameof(regionalRadius));
        DetailedRadius = detailedRadius;
        RegionalRadius = regionalRadius;
    }

    public int DetailedRadius { get; }
    public int RegionalRadius { get; }
}

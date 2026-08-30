namespace RpgWorld.Simulation.Engine;

public static class SimulationSystemFrequencies
{
    public static readonly TimeSpan Movement = TimeSpan.FromMilliseconds(100);
    public static readonly TimeSpan Combat = TimeSpan.FromMilliseconds(250);
    public static readonly TimeSpan NpcDecisions = TimeSpan.FromSeconds(1);
    public static readonly TimeSpan Economy = TimeSpan.FromMinutes(1);
    public static readonly TimeSpan Population = TimeSpan.FromMinutes(10);
    public static readonly TimeSpan Diplomacy = TimeSpan.FromMinutes(30);
    public static readonly TimeSpan GlobalEvents = TimeSpan.FromHours(1);
}

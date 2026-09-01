namespace RpgWorld.Simulation.Engine;

public interface ISimulationRandom
{
    int Next(int exclusiveMaximum);
}

public sealed class SystemSimulationRandom : ISimulationRandom
{
    public int Next(int exclusiveMaximum)
    {
        if (exclusiveMaximum <= 0) throw new ArgumentOutOfRangeException(nameof(exclusiveMaximum));
        return Random.Shared.Next(exclusiveMaximum);
    }
}

public sealed class SeededSimulationRandom(int seed) : ISimulationRandom
{
    private readonly Random _random = new(seed);
    private readonly Lock _lock = new();

    public int Next(int exclusiveMaximum)
    {
        if (exclusiveMaximum <= 0) throw new ArgumentOutOfRangeException(nameof(exclusiveMaximum));
        lock (_lock) return _random.Next(exclusiveMaximum);
    }
}

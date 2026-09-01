namespace RpgWorld.Simulation.Engine;

public sealed class SimulationTickWorkload
{
    private long _actorsProcessed;

    public SimulationTickWorkload(int activeChunks)
    {
        if (activeChunks < 0) throw new ArgumentOutOfRangeException(nameof(activeChunks));
        ActiveChunks = activeChunks;
    }

    public int ActiveChunks { get; }
    public long ActorsProcessed => Interlocked.Read(ref _actorsProcessed);

    public void RecordActors(int count)
    {
        if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));
        Interlocked.Add(ref _actorsProcessed, count);
    }
}

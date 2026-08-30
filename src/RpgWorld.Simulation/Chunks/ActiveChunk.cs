using RpgWorld.Domain.Worlds;

namespace RpgWorld.Simulation.Chunks;

public sealed class ActiveChunk
{
    internal ActiveChunk(
        Chunk chunk,
        IReadOnlyList<Tile> tiles,
        DateTimeOffset activatedAtUtc)
    {
        Chunk = chunk;
        Tiles = tiles;
        ActivatedAtUtc = activatedAtUtc;
        LastRelevantAtUtc = activatedAtUtc;
    }

    public Chunk Chunk { get; }

    public IReadOnlyList<Tile> Tiles { get; }

    public DateTimeOffset ActivatedAtUtc { get; }

    public DateTimeOffset LastRelevantAtUtc { get; private set; }

    internal void MarkRelevant(DateTimeOffset observedAtUtc)
    {
        if (observedAtUtc > LastRelevantAtUtc)
        {
            LastRelevantAtUtc = observedAtUtc;
        }
    }
}

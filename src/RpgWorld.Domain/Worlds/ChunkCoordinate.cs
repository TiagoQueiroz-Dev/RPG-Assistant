namespace RpgWorld.Domain.Worlds;

public readonly record struct ChunkCoordinate
{
    public ChunkCoordinate(int x, int y)
    {
        if (x < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(x), "Chunk X cannot be negative.");
        }

        if (y < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(y), "Chunk Y cannot be negative.");
        }

        X = x;
        Y = y;
    }

    public int X { get; }

    public int Y { get; }
}


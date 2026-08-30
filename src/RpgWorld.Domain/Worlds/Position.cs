namespace RpgWorld.Domain.Worlds;

public readonly record struct Position
{
    public Position(Guid worldId, int x, int y)
    {
        if (worldId == Guid.Empty)
        {
            throw new ArgumentException("World identifier cannot be empty.", nameof(worldId));
        }

        if (x < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(x), "X cannot be negative.");
        }

        if (y < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(y), "Y cannot be negative.");
        }

        WorldId = worldId;
        X = x;
        Y = y;
    }

    public Guid WorldId { get; }

    public int X { get; }

    public int Y { get; }
}


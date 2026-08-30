namespace RpgWorld.Domain.Worlds;

public sealed class World : AggregateRoot
{
    public const int DefaultChunkSize = 32;
    public const int MaximumDimension = 1_000_000;

    private World()
    {
    }

    private World(Guid id, string name, int width, int height, int chunkSize)
    {
        Id = id;
        Name = name;
        Width = width;
        Height = height;
        ChunkSize = chunkSize;
    }

    public Guid Id { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public int Width { get; private set; }

    public int Height { get; private set; }

    public int ChunkSize { get; private set; }

    public int ChunkColumns => ((Width - 1) / ChunkSize) + 1;

    public int ChunkRows => ((Height - 1) / ChunkSize) + 1;

    public static World Create(
        string name,
        int width,
        int height,
        int chunkSize = DefaultChunkSize)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("World name cannot be empty.", nameof(name));
        }

        ValidateDimension(width, nameof(width));
        ValidateDimension(height, nameof(height));

        if (chunkSize is <= 0 or > 1024)
        {
            throw new ArgumentOutOfRangeException(
                nameof(chunkSize),
                "Chunk size must be between 1 and 1024 tiles.");
        }

        return new World(
            Guid.CreateVersion7(),
            name.Trim(),
            width,
            height,
            chunkSize);
    }

    public Position PositionAt(int x, int y)
    {
        var position = new Position(Id, x, y);
        EnsureContains(position);
        return position;
    }

    public bool Contains(Position position) =>
        position.WorldId == Id &&
        position.X >= 0 &&
        position.X < Width &&
        position.Y >= 0 &&
        position.Y < Height;

    public ChunkCoordinate ChunkAt(Position position)
    {
        EnsureContains(position);
        return new ChunkCoordinate(position.X / ChunkSize, position.Y / ChunkSize);
    }

    public Chunk CreateChunk(ChunkCoordinate coordinate)
    {
        if (coordinate.X >= ChunkColumns || coordinate.Y >= ChunkRows)
        {
            throw new ArgumentOutOfRangeException(
                nameof(coordinate),
                "Chunk coordinate is outside the world.");
        }

        var originX = coordinate.X * ChunkSize;
        var originY = coordinate.Y * ChunkSize;

        return Chunk.Create(
            Id,
            coordinate,
            originX,
            originY,
            Math.Min(ChunkSize, Width - originX),
            Math.Min(ChunkSize, Height - originY));
    }

    public Tile CreateTile(
        Position position,
        string terrainCode,
        string biomeCode,
        short elevation,
        decimal temperatureCelsius,
        decimal humidity)
    {
        EnsureContains(position);
        return Tile.Create(
            position,
            terrainCode,
            biomeCode,
            elevation,
            temperatureCelsius,
            humidity);
    }

    private void EnsureContains(Position position)
    {
        if (!Contains(position))
        {
            throw new ArgumentOutOfRangeException(
                nameof(position),
                "Position is outside this world.");
        }
    }

    private static void ValidateDimension(int value, string parameterName)
    {
        if (value is <= 0 or > MaximumDimension)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                $"World dimension must be between 1 and {MaximumDimension}.");
        }
    }
}


namespace RpgWorld.Domain.Worlds;

public sealed class Chunk
{
    private Chunk()
    {
    }

    private Chunk(
        Guid id,
        Guid worldId,
        ChunkCoordinate coordinate,
        int originX,
        int originY,
        int width,
        int height)
    {
        Id = id;
        WorldId = worldId;
        CoordinateX = coordinate.X;
        CoordinateY = coordinate.Y;
        OriginX = originX;
        OriginY = originY;
        Width = width;
        Height = height;
    }

    public Guid Id { get; private set; }

    public Guid WorldId { get; private set; }

    public int CoordinateX { get; private set; }

    public int CoordinateY { get; private set; }

    public int OriginX { get; private set; }

    public int OriginY { get; private set; }

    public int Width { get; private set; }

    public int Height { get; private set; }

    public ChunkCoordinate Coordinate => new(CoordinateX, CoordinateY);

    public bool Contains(Position position) =>
        position.WorldId == WorldId &&
        position.X >= OriginX &&
        position.X < OriginX + Width &&
        position.Y >= OriginY &&
        position.Y < OriginY + Height;

    internal static Chunk Create(
        Guid worldId,
        ChunkCoordinate coordinate,
        int originX,
        int originY,
        int width,
        int height) =>
        new(
            Guid.CreateVersion7(),
            worldId,
            coordinate,
            originX,
            originY,
            width,
            height);
}


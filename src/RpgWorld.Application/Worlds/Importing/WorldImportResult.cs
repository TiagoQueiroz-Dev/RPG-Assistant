namespace RpgWorld.Application.Worlds.Importing;

public sealed record WorldImportResult(
    Guid WorldId,
    string Name,
    int Width,
    int Height,
    int ChunkCount,
    int TileCount,
    string ImageFormat,
    string Status);

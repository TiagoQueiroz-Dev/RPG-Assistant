namespace RpgWorld.Api.WorldMaps;

public sealed record WorldMapView(
    Guid WorldId,
    string Name,
    int Width,
    int Height,
    int ChunkSize,
    IReadOnlyList<WorldMapChunkView> Chunks);

public sealed record WorldMapChunkView(
    int X,
    int Y,
    int OriginX,
    int OriginY,
    int Width,
    int Height,
    IReadOnlyList<WorldMapTileView> Tiles);

public sealed record WorldMapTileView(
    int X,
    int Y,
    string TerrainCode,
    string BiomeCode,
    short Elevation);

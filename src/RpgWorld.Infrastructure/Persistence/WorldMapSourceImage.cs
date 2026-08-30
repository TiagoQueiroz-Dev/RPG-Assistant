namespace RpgWorld.Infrastructure.Persistence;

public sealed class WorldMapSourceImage
{
    private WorldMapSourceImage()
    {
    }

    public WorldMapSourceImage(
        Guid worldId,
        string fileName,
        string mediaType,
        string sha256,
        int pixelWidth,
        int pixelHeight,
        int gridResolution,
        byte[] data,
        DateTimeOffset importedAtUtc)
    {
        Id = Guid.CreateVersion7();
        WorldId = worldId;
        FileName = fileName;
        MediaType = mediaType;
        Sha256 = sha256;
        PixelWidth = pixelWidth;
        PixelHeight = pixelHeight;
        GridResolution = gridResolution;
        Data = data;
        ImportedAtUtc = importedAtUtc;
    }

    public Guid Id { get; private set; }
    public Guid WorldId { get; private set; }
    public string FileName { get; private set; } = string.Empty;
    public string MediaType { get; private set; } = string.Empty;
    public string Sha256 { get; private set; } = string.Empty;
    public int PixelWidth { get; private set; }
    public int PixelHeight { get; private set; }
    public int GridResolution { get; private set; }
    public byte[] Data { get; private set; } = [];
    public DateTimeOffset ImportedAtUtc { get; private set; }
}

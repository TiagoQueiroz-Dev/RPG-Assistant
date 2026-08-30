namespace RpgWorld.Application.Worlds.Importing;

public sealed record WorldImportRequest(
    string Name,
    string FileName,
    byte[] ImageData,
    int GridResolution);

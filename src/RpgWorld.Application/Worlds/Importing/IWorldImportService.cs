namespace RpgWorld.Application.Worlds.Importing;

public interface IWorldImportService
{
    Task<WorldImportResult> ImportAsync(
        WorldImportRequest request,
        CancellationToken cancellationToken = default);
}

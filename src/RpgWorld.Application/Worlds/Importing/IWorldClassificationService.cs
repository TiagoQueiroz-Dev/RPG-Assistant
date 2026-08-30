namespace RpgWorld.Application.Worlds.Importing;

public interface IWorldClassificationService
{
    Task<WorldClassificationResult> ReprocessAsync(Guid worldId, CancellationToken cancellationToken = default);

    Task ConfirmManualAsync(
        Guid worldId,
        int x,
        int y,
        string biomeCode,
        CancellationToken cancellationToken = default);
}

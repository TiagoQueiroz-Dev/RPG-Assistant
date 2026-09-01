namespace RpgWorld.Application.Worlds.Visibility;

public sealed record PlayerTileVisibility(int X, int Y, string State);

public sealed record PlayerVisibilityView(
    Guid PlayerActorId,
    Guid WorldId,
    int PlayerX,
    int PlayerY,
    int PerceptionRadius,
    IReadOnlyList<PlayerTileVisibility> Tiles);

public interface IPlayerVisibilityService
{
    Task<PlayerVisibilityView> GetAsync(
        Guid playerActorId,
        CancellationToken cancellationToken = default);

    Task RefreshAsync(
        Guid playerActorId,
        DateTimeOffset observedAtUtc,
        CancellationToken cancellationToken = default);
}

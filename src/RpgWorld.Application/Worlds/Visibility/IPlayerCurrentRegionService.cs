namespace RpgWorld.Application.Worlds.Visibility;

public sealed record PlayerRegionEntityView(
    Guid Id,
    string Name,
    string Kind,
    string Category,
    int X,
    int Y,
    int Distance,
    int Relevance);

public sealed record PlayerCurrentRegionView(
    Guid PlayerActorId,
    Guid WorldId,
    string WorldName,
    string CharacterName,
    Guid RegionId,
    string RegionKind,
    string RegionName,
    int X,
    int Y,
    int PerceptionRadius,
    IReadOnlyList<PlayerRegionEntityView> VisibleEntities,
    IReadOnlyList<PlayerVisibleStructureView> VisibleEstablishments,
    IReadOnlyList<PlayerVisibleEventView> RelevantEvents);

public interface IPlayerCurrentRegionService
{
    Task<PlayerCurrentRegionView> GetAsync(
        Guid playerActorId,
        CancellationToken cancellationToken = default);
}

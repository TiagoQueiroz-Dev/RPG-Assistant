namespace RpgWorld.Application.Worlds.Visibility;

public sealed record PlayerVisibleEntityView(
    Guid Id,
    string Name,
    string Kind,
    int X,
    int Y,
    int Distance);

public sealed record PlayerVisibleStructureView(
    Guid Id,
    string Kind,
    int X,
    int Y);

public sealed record PlayerVisibleEventView(
    Guid Id,
    string Type,
    DateTimeOffset TimestampUtc,
    int? X,
    int? Y);

public sealed record PlayerWorldView(
    Guid PlayerActorId,
    Guid WorldId,
    string WorldName,
    string CharacterName,
    int X,
    int Y,
    int PerceptionRadius,
    IReadOnlyList<PlayerVisibleEntityView> VisibleEntities,
    IReadOnlyList<PlayerVisibleStructureView> VisibleStructures,
    IReadOnlyList<PlayerVisibleEventView> RelevantEvents);

public interface IPlayerWorldViewService
{
    Task<PlayerWorldView> GetAsync(
        Guid playerActorId,
        CancellationToken cancellationToken = default);
}

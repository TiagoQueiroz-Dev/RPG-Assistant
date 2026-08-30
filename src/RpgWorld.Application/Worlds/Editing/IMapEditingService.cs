namespace RpgWorld.Application.Worlds.Editing;

public interface IMapEditingService
{
    Task<MapEditResult> PaintAsync(Guid worldId, MapPaintRequest request, CancellationToken cancellationToken = default);

    Task<MapEditResult?> UndoAsync(Guid worldId, CancellationToken cancellationToken = default);

    Task<MapEditResult?> RedoAsync(Guid worldId, CancellationToken cancellationToken = default);
}

using System.Text.Json;
using RpgWorld.Application.Worlds.Editing;

namespace RpgWorld.Infrastructure.Persistence;

public sealed class MapEditOperation
{
    private MapEditOperation() { }

    public MapEditOperation(
        Guid worldId,
        MapBrushKind brush,
        JsonDocument changes,
        int affectedTiles,
        DateTimeOffset createdAtUtc)
    {
        Id = Guid.CreateVersion7();
        WorldId = worldId;
        Brush = brush;
        Changes = changes;
        AffectedTiles = affectedTiles;
        CreatedAtUtc = createdAtUtc;
        Status = MapEditOperationStatus.Applied;
    }

    public Guid Id { get; private set; }
    public Guid WorldId { get; private set; }
    public MapBrushKind Brush { get; private set; }
    public JsonDocument Changes { get; private set; } = null!;
    public int AffectedTiles { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public MapEditOperationStatus Status { get; private set; }

    public void MarkApplied() => Status = MapEditOperationStatus.Applied;
    public void MarkUndone() => Status = MapEditOperationStatus.Undone;
    public void Discard() => Status = MapEditOperationStatus.Discarded;
}

public enum MapEditOperationStatus
{
    Applied,
    Undone,
    Discarded
}

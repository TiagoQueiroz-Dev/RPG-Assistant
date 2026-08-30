using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RpgWorld.Application.Worlds.Editing;
using RpgWorld.Domain.Worlds;
using RpgWorld.Domain.Worlds.Definitions;
using RpgWorld.Infrastructure.Persistence;

namespace RpgWorld.Infrastructure.Worlds.Editing;

public sealed class MapEditingService(
    RpgWorldDbContext dbContext,
    IWorldDefinitionCatalog definitions,
    TimeProvider timeProvider) : IMapEditingService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<MapEditResult> PaintAsync(
        Guid worldId,
        MapPaintRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateRequest(request);
        var world = await dbContext.Worlds.SingleOrDefaultAsync(
            candidate => candidate.Id == worldId,
            cancellationToken)
            ?? throw new KeyNotFoundException("World was not found.");
        var center = world.PositionAt(request.CenterX, request.CenterY);
        var lowerOffset = (request.Size - 1) / 2;
        var upperOffset = request.Size / 2;
        var minX = Math.Max(0, center.X - lowerOffset);
        var maxX = Math.Min(world.Width - 1, center.X + upperOffset);
        var minY = Math.Max(0, center.Y - lowerOffset);
        var maxY = Math.Min(world.Height - 1, center.Y + upperOffset);
        var tiles = await dbContext.Tiles
            .Where(tile =>
                tile.WorldId == worldId &&
                tile.X >= minX && tile.X <= maxX &&
                tile.Y >= minY && tile.Y <= maxY)
            .ToArrayAsync(cancellationToken);

        if (tiles.Length == 0)
        {
            throw new InvalidOperationException("The selected area contains no persisted tiles.");
        }

        var cityId = request.Brush == MapBrushKind.City ? Guid.CreateVersion7() : (Guid?)null;
        var changes = new List<MapTileChange>(tiles.Length);

        foreach (var tile in tiles)
        {
            var before = Capture(tile);
            ApplyBrush(tile, request.Brush, cityId);
            changes.Add(new MapTileChange(before, Capture(tile)));
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var discarded = await dbContext.MapEditOperations
            .Where(operation => operation.WorldId == worldId && operation.Status == MapEditOperationStatus.Undone)
            .ToArrayAsync(cancellationToken);
        foreach (var operation in discarded) operation.Discard();

        var edit = new MapEditOperation(
            worldId,
            request.Brush,
            JsonSerializer.SerializeToDocument(changes, JsonOptions),
            tiles.Length,
            timeProvider.GetUtcNow());
        dbContext.MapEditOperations.Add(edit);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Result(edit);
    }

    public Task<MapEditResult?> UndoAsync(Guid worldId, CancellationToken cancellationToken = default) =>
        RestoreAsync(worldId, undo: true, cancellationToken);

    public Task<MapEditResult?> RedoAsync(Guid worldId, CancellationToken cancellationToken = default) =>
        RestoreAsync(worldId, undo: false, cancellationToken);

    private async Task<MapEditResult?> RestoreAsync(
        Guid worldId,
        bool undo,
        CancellationToken cancellationToken)
    {
        var expectedStatus = undo ? MapEditOperationStatus.Applied : MapEditOperationStatus.Undone;
        var query = dbContext.MapEditOperations
            .Where(operation => operation.WorldId == worldId && operation.Status == expectedStatus);
        var operation = undo
            ? await query.OrderByDescending(item => item.CreatedAtUtc).FirstOrDefaultAsync(cancellationToken)
            : await query.OrderBy(item => item.CreatedAtUtc).FirstOrDefaultAsync(cancellationToken);

        if (operation is null) return null;

        var changes = operation.Changes.Deserialize<MapTileChange[]>(JsonOptions) ?? [];
        var coordinates = changes.Select(change => (change.Before.X, change.Before.Y)).ToHashSet();
        var tiles = await dbContext.Tiles
            .Where(tile => tile.WorldId == worldId)
            .ToArrayAsync(cancellationToken);
        var tileIndex = tiles
            .Where(tile => coordinates.Contains((tile.X, tile.Y)))
            .ToDictionary(tile => (tile.X, tile.Y));

        foreach (var change in changes)
        {
            if (!tileIndex.TryGetValue((change.Before.X, change.Before.Y), out var tile))
                throw new InvalidOperationException("An edited tile no longer exists.");
            Restore(tile, undo ? change.Before : change.After);
        }

        if (undo) operation.MarkUndone(); else operation.MarkApplied();
        await dbContext.SaveChangesAsync(cancellationToken);
        return Result(operation);
    }

    private void ApplyBrush(Tile tile, MapBrushKind brush, Guid? cityId)
    {
        var biomeCode = brush switch
        {
            MapBrushKind.Forest => "forest",
            MapBrushKind.Water => "ocean",
            MapBrushKind.Mountain => "mountain",
            MapBrushKind.Desert => "desert",
            MapBrushKind.City => null,
            _ => throw new ArgumentOutOfRangeException(nameof(brush))
        };

        if (biomeCode is not null)
        {
            tile.SetEnvironment(
                biomeCode,
                definitions,
                tile.Elevation,
                tile.TemperatureCelsius,
                tile.Humidity);
        }
        else
        {
            tile.AssignStructure(cityId);
        }
    }

    private void Restore(Tile tile, MapTileState state) =>
        tile.RestoreMapState(
            state.BiomeCode,
            state.ClassificationOrigin,
            state.ClassificationConfidence,
            state.StructureId,
            definitions);

    private static MapTileState Capture(Tile tile) => new(
        tile.X,
        tile.Y,
        tile.BiomeCode,
        tile.BiomeClassificationOrigin,
        tile.BiomeClassificationConfidence,
        tile.StructureId);

    private static MapEditResult Result(MapEditOperation operation) => new(
        operation.Id,
        operation.WorldId,
        operation.AffectedTiles,
        operation.Status.ToString().ToLowerInvariant());

    private static void ValidateRequest(MapPaintRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!Enum.IsDefined(request.Brush))
            throw new ArgumentOutOfRangeException(nameof(request), "Unknown map brush.");
        if (request.Size is < 1 or > 16)
            throw new ArgumentOutOfRangeException(nameof(request), "Brush size must be between 1 and 16.");
    }

    private sealed record MapTileChange(MapTileState Before, MapTileState After);

    private sealed record MapTileState(
        int X,
        int Y,
        string BiomeCode,
        BiomeClassificationOrigin ClassificationOrigin,
        decimal? ClassificationConfidence,
        Guid? StructureId);
}

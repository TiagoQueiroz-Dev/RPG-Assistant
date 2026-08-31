using System.Text.Json;

namespace RpgWorld.Application.Worlds.Events;

public sealed class WorldEventService(IWorldEventRepository repository) : IWorldEventService
{
    public async Task<WorldEventTimelinePage> SearchAsync(
        WorldEventQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (query.WorldId == Guid.Empty) throw new ArgumentException("World identifier is required.", nameof(query));
        if (query.Page <= 0) throw new ArgumentOutOfRangeException(nameof(query.Page));
        if (query.PageSize is <= 0 or > 200) throw new ArgumentOutOfRangeException(nameof(query.PageSize));
        if (query.ActorId == Guid.Empty) throw new ArgumentException("Actor identifier cannot be empty.", nameof(query));
        if (query.FromUtc > query.ToUtc) throw new ArgumentException("Timeline start cannot be after its end.", nameof(query));
        if (query.PositionX.HasValue != query.PositionY.HasValue || query.PositionX < 0 || query.PositionY < 0)
            throw new ArgumentException("Position requires valid X and Y coordinates.", nameof(query));
        if (!Enum.IsDefined(query.SortOrder)) throw new ArgumentOutOfRangeException(nameof(query.SortOrder));
        if (!await repository.WorldExistsAsync(query.WorldId, cancellationToken))
            throw new KeyNotFoundException($"World '{query.WorldId}' was not found.");
        var result = await repository.SearchAsync(query with { Type = NormalizeType(query.Type) }, cancellationToken);
        var items = result.Items.Select(item => new WorldEventTimelineItem(
            item.Id,
            item.WorldId,
            item.Type,
            item.TimestampUtc,
            item.Position is { } position ? new(position.X, position.Y) : null,
            item.ActorIds,
            ParsePayload(item.Payload),
            item.PayloadVersion)).ToArray();
        var totalPages = result.TotalCount == 0 ? 0 : checked((int)Math.Ceiling(result.TotalCount / (decimal)result.PageSize));
        return new WorldEventTimelinePage(items, result.Page, result.PageSize, result.TotalCount, totalPages);
    }

    private static string? NormalizeType(string? type) =>
        string.IsNullOrWhiteSpace(type) ? null : type.Trim();

    private static JsonElement ParsePayload(string payload)
    {
        using var document = JsonDocument.Parse(payload);
        return document.RootElement.Clone();
    }
}

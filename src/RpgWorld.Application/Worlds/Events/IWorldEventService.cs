using System.Text.Json;
using RpgWorld.Domain.Worlds.Events;

namespace RpgWorld.Application.Worlds.Events;

public enum WorldEventSortOrder { NewestFirst, OldestFirst }

public sealed record WorldEventQuery(
    Guid WorldId,
    int Page = 1,
    int PageSize = 50,
    string? Type = null,
    Guid? ActorId = null,
    DateTimeOffset? FromUtc = null,
    DateTimeOffset? ToUtc = null,
    int? PositionX = null,
    int? PositionY = null,
    WorldEventSortOrder SortOrder = WorldEventSortOrder.NewestFirst,
    Guid? CorrelationId = null);

public sealed record WorldEventPage(
    IReadOnlyList<WorldEvent> Items,
    int Page,
    int PageSize,
    long TotalCount);

public sealed record WorldEventTimelinePosition(int X, int Y);

public sealed record WorldEventTimelineItem(
    Guid Id,
    Guid WorldId,
    string Type,
    DateTimeOffset TimestampUtc,
    WorldEventTimelinePosition? Position,
    IReadOnlyList<Guid> Actors,
    JsonElement Payload,
    int PayloadVersion,
    Guid CorrelationId,
    Guid? CausationId,
    int CausalityDepth);

public sealed record WorldEventTimelinePage(
    IReadOnlyList<WorldEventTimelineItem> Items,
    int Page,
    int PageSize,
    long TotalCount,
    int TotalPages);

public interface IWorldEventRepository
{
    Task<bool> WorldExistsAsync(Guid worldId, CancellationToken cancellationToken = default);
    Task<WorldEventPage> SearchAsync(WorldEventQuery query, CancellationToken cancellationToken = default);
}

public interface IWorldEventService
{
    Task<WorldEventTimelinePage> SearchAsync(WorldEventQuery query, CancellationToken cancellationToken = default);
}

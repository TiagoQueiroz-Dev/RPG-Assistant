using Microsoft.AspNetCore.SignalR;
using RpgWorld.Application.Realtime;
using RpgWorld.Application.Worlds.Visibility;

namespace RpgWorld.Api.Realtime;

public sealed class SignalRWorldUpdatePublisher(
    IHubContext<WorldHub, IWorldHubClient> hubContext,
    IPlayerVisibilityService visibilityService)
    : IWorldUpdatePublisher
{
    public Task PublishToWorldAsync(
        WorldUpdateMessage message,
        CancellationToken cancellationToken = default) =>
        SendAsync(RealtimeGroups.World(message.WorldId), message, cancellationToken);

    public Task PublishToChunkAsync(
        Guid chunkId,
        WorldUpdateMessage message,
        CancellationToken cancellationToken = default) =>
        SendAsync(RealtimeGroups.Chunk(chunkId), message, cancellationToken);

    public Task PublishToPlayerAsync(
        Guid playerId,
        WorldUpdateMessage message,
        CancellationToken cancellationToken = default) =>
        SendAsync(RealtimeGroups.Player(playerId), message, cancellationToken);

    public async Task PublishToGameMasterAsync(
        WorldUpdateMessage message,
        CancellationToken cancellationToken = default)
    {
        await SendAsync(RealtimeGroups.GameMaster(message.WorldId), message, cancellationToken);
        if (!TryPosition(message, "destinationX", "destinationY", out var x, out var y) &&
            !TryPosition(message, "x", "y", out x, out y)) return;
        var recipients = await visibilityService.ListPlayersSeeingAsync(message.WorldId, x, y, cancellationToken);
        var sourceActorId = message.Data.TryGetValue("actorId", out var actorValue) && Guid.TryParse(actorValue, out var parsed)
            ? parsed : (Guid?)null;
        var groups = recipients.Where(value => value != sourceActorId)
            .Select(RealtimeGroups.Player).Distinct(StringComparer.Ordinal).ToArray();
        if (groups.Length > 0) await hubContext.Clients.Groups(groups).WorldUpdated(message);
        if (!TryPosition(message, "originX", "originY", out var originX, out var originY)) return;
        var originRecipients = await visibilityService.ListPlayersSeeingAsync(
            message.WorldId, originX, originY, cancellationToken);
        var destinationSet = recipients.ToHashSet();
        var leavingGroups = originRecipients.Where(value => value != sourceActorId && !destinationSet.Contains(value))
            .Select(RealtimeGroups.Player).Distinct(StringComparer.Ordinal).ToArray();
        if (leavingGroups.Length == 0) return;
        var invalidation = new WorldUpdateMessage(
            message.MessageId, message.WorldId, "visibility.changed", message.OccurredAtUtc,
            new Dictionary<string, string?>
            {
                ["actorId"] = message.Data.GetValueOrDefault("actorId"),
                ["actorKind"] = message.Data.GetValueOrDefault("actorKind")
            });
        await hubContext.Clients.Groups(leavingGroups).WorldUpdated(invalidation);
    }

    private Task SendAsync(
        string groupName,
        WorldUpdateMessage message,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);
        return hubContext.Clients.Group(groupName).WorldUpdated(message);
    }

    private static bool TryPosition(
        WorldUpdateMessage message,
        string xKey,
        string yKey,
        out int x,
        out int y)
    {
        var xValue = message.Data.GetValueOrDefault(xKey);
        var yValue = message.Data.GetValueOrDefault(yKey);
        var parsedX = int.TryParse(xValue, out x);
        var parsedY = int.TryParse(yValue, out y);
        return parsedX && parsedY;
    }
}

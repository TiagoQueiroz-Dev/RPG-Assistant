using Microsoft.AspNetCore.SignalR;
using RpgWorld.Application.Realtime;

namespace RpgWorld.Api.Realtime;

public sealed class SignalRWorldUpdatePublisher(
    IHubContext<WorldHub, IWorldHubClient> hubContext)
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

    public Task PublishToGameMasterAsync(
        WorldUpdateMessage message,
        CancellationToken cancellationToken = default) =>
        SendAsync(RealtimeGroups.GameMaster(message.WorldId), message, cancellationToken);

    private Task SendAsync(
        string groupName,
        WorldUpdateMessage message,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);
        return hubContext.Clients.Group(groupName).WorldUpdated(message);
    }
}


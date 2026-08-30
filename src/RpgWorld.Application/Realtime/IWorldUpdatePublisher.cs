namespace RpgWorld.Application.Realtime;

public interface IWorldUpdatePublisher
{
    Task PublishToWorldAsync(
        WorldUpdateMessage message,
        CancellationToken cancellationToken = default);

    Task PublishToChunkAsync(
        Guid chunkId,
        WorldUpdateMessage message,
        CancellationToken cancellationToken = default);

    Task PublishToPlayerAsync(
        Guid playerId,
        WorldUpdateMessage message,
        CancellationToken cancellationToken = default);

    Task PublishToGameMasterAsync(
        WorldUpdateMessage message,
        CancellationToken cancellationToken = default);
}


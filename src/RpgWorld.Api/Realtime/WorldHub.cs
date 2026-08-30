using Microsoft.AspNetCore.SignalR;
using RpgWorld.Application.Realtime;

namespace RpgWorld.Api.Realtime;

public sealed class WorldHub(IRealtimeSubscriptionAuthorizer authorizer)
    : Hub<IWorldHubClient>
{
    public Task JoinWorld(Guid worldId) => JoinAsync(
        new RealtimeSubscription(RealtimeAudience.World, worldId),
        RealtimeGroups.World(worldId));

    public Task LeaveWorld(Guid worldId) => LeaveAsync(RealtimeGroups.World(worldId));

    public Task JoinChunk(Guid chunkId) => JoinAsync(
        new RealtimeSubscription(RealtimeAudience.Chunk, chunkId),
        RealtimeGroups.Chunk(chunkId));

    public Task LeaveChunk(Guid chunkId) => LeaveAsync(RealtimeGroups.Chunk(chunkId));

    public Task JoinPlayer(Guid playerId) => JoinAsync(
        new RealtimeSubscription(RealtimeAudience.Player, playerId),
        RealtimeGroups.Player(playerId));

    public Task LeavePlayer(Guid playerId) => LeaveAsync(RealtimeGroups.Player(playerId));

    public Task JoinGameMaster(Guid worldId) => JoinAsync(
        new RealtimeSubscription(RealtimeAudience.GameMaster, worldId),
        RealtimeGroups.GameMaster(worldId));

    public Task LeaveGameMaster(Guid worldId) =>
        LeaveAsync(RealtimeGroups.GameMaster(worldId));

    private async Task JoinAsync(
        RealtimeSubscription subscription,
        string groupName)
    {
        var authorized = await authorizer.CanSubscribeAsync(
            Context.User,
            subscription,
            Context.ConnectionAborted);

        if (!authorized)
        {
            throw new HubException("The connection is not authorized for this group.");
        }

        await Groups.AddToGroupAsync(
            Context.ConnectionId,
            groupName,
            Context.ConnectionAborted);
    }

    private Task LeaveAsync(string groupName) => Groups.RemoveFromGroupAsync(
        Context.ConnectionId,
        groupName,
        Context.ConnectionAborted);
}


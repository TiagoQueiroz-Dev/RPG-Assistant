using System.Security.Claims;

namespace RpgWorld.Application.Realtime;

public enum RealtimeAudience
{
    World,
    Chunk,
    Player,
    GameMaster
}

public sealed record RealtimeSubscription(
    RealtimeAudience Audience,
    Guid TargetId);

public interface IRealtimeSubscriptionAuthorizer
{
    Task<bool> CanSubscribeAsync(
        ClaimsPrincipal? user,
        RealtimeSubscription subscription,
        CancellationToken cancellationToken = default);
}


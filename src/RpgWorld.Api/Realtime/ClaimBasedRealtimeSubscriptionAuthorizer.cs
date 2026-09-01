using System.Security.Claims;
using RpgWorld.Application.Realtime;

namespace RpgWorld.Api.Realtime;

public sealed class ClaimBasedRealtimeSubscriptionAuthorizer
    : IRealtimeSubscriptionAuthorizer
{
    public Task<bool> CanSubscribeAsync(
        ClaimsPrincipal? user,
        RealtimeSubscription subscription,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (user?.Identity?.IsAuthenticated != true || subscription.TargetId == Guid.Empty)
        {
            return Task.FromResult(false);
        }

        var authorized = subscription.Audience switch
        {
            RealtimeAudience.World =>
                HasIdentifierClaim(user, RealtimeClaimTypes.GameMasterWorld, subscription.TargetId),
            RealtimeAudience.Chunk =>
                false,
            RealtimeAudience.Player =>
                HasIdentifierClaim(user, ClaimTypes.NameIdentifier, subscription.TargetId) ||
                HasIdentifierClaim(user, "sub", subscription.TargetId) ||
                HasIdentifierClaim(user, RealtimeClaimTypes.PlayerActor, subscription.TargetId),
            RealtimeAudience.GameMaster =>
                HasIdentifierClaim(
                    user,
                    RealtimeClaimTypes.GameMasterWorld,
                    subscription.TargetId),
            _ => false
        };

        return Task.FromResult(authorized);
    }

    private static bool HasIdentifierClaim(
        ClaimsPrincipal user,
        string claimType,
        Guid expected) =>
        user.FindAll(claimType).Any(claim =>
            Guid.TryParse(claim.Value, out var actual) && actual == expected);
}

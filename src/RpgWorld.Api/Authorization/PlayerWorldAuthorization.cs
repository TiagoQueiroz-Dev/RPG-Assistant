using System.Security.Claims;
using RpgWorld.Api.Realtime;

namespace RpgWorld.Api.Authorization;

public static class PlayerWorldAuthorization
{
    public static bool HasContext(ClaimsPrincipal? user, Guid worldId, Guid playerActorId)
    {
        if (user?.Identity?.IsAuthenticated != true || worldId == Guid.Empty || playerActorId == Guid.Empty) return false;
        if (GameMasterWorldAuthorization.HasContext(user, worldId)) return true;
        return HasClaim(user, RealtimeClaimTypes.World, worldId) &&
            HasClaim(user, RealtimeClaimTypes.PlayerActor, playerActorId);
    }

    private static bool HasClaim(ClaimsPrincipal user, string claimType, Guid expected) =>
        user.FindAll(claimType).Any(claim => Guid.TryParse(claim.Value, out var actual) && actual == expected);
}

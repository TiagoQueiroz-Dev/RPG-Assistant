using System.Security.Claims;
using RpgWorld.Api.Realtime;

namespace RpgWorld.Api.Authorization;

public static class GameMasterWorldAuthorization
{
    public static bool HasAnyContext(ClaimsPrincipal? user) =>
        user?.Identity?.IsAuthenticated == true &&
        user!.FindAll(RealtimeClaimTypes.GameMasterWorld).Any(claim =>
            Guid.TryParse(claim.Value, out var claimedWorldId) && claimedWorldId != Guid.Empty);

    public static bool HasContext(ClaimsPrincipal? user, Guid worldId) =>
        HasAnyContext(user) &&
        worldId != Guid.Empty &&
        user!.FindAll(RealtimeClaimTypes.GameMasterWorld).Any(claim =>
            Guid.TryParse(claim.Value, out var claimedWorldId) && claimedWorldId == worldId);
}

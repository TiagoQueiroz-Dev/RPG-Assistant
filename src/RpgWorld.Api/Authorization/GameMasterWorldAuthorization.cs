using System.Security.Claims;
using RpgWorld.Api.Realtime;

namespace RpgWorld.Api.Authorization;

public static class GameMasterWorldAuthorization
{
    public static bool HasContext(ClaimsPrincipal? user, Guid worldId) =>
        user?.Identity?.IsAuthenticated == true &&
        worldId != Guid.Empty &&
        user.FindAll(RealtimeClaimTypes.GameMasterWorld).Any(claim =>
            Guid.TryParse(claim.Value, out var claimedWorldId) && claimedWorldId == worldId);
}

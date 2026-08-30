using System.Security.Claims;
using RpgWorld.Api.Authorization;
using RpgWorld.Api.Realtime;

namespace RpgWorld.Api.Tests.Authorization;

public sealed class GameMasterWorldAuthorizationTests
{
    [Fact]
    public void Only_authenticated_master_claim_for_requested_world_is_accepted()
    {
        var worldId = Guid.NewGuid();
        var otherWorldId = Guid.NewGuid();
        var authorized = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(RealtimeClaimTypes.GameMasterWorld, worldId.ToString())],
            "test"));
        var wrongWorld = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(RealtimeClaimTypes.GameMasterWorld, otherWorldId.ToString())],
            "test"));

        Assert.True(GameMasterWorldAuthorization.HasContext(authorized, worldId));
        Assert.False(GameMasterWorldAuthorization.HasContext(wrongWorld, worldId));
        Assert.False(GameMasterWorldAuthorization.HasContext(new ClaimsPrincipal(), worldId));
        Assert.False(GameMasterWorldAuthorization.HasContext(null, worldId));
    }
}

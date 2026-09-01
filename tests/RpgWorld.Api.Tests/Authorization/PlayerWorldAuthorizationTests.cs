using System.Security.Claims;
using RpgWorld.Api.Authorization;
using RpgWorld.Api.Realtime;

namespace RpgWorld.Api.Tests.Authorization;

public sealed class PlayerWorldAuthorizationTests
{
    [Fact]
    public void Requires_matching_authenticated_world_and_player_actor_claims()
    {
        var worldId = Guid.NewGuid();
        var playerId = Guid.NewGuid();
        var authorized = Principal(
            new Claim(RealtimeClaimTypes.World, worldId.ToString()),
            new Claim(RealtimeClaimTypes.PlayerActor, playerId.ToString()));
        var wrongPlayer = Principal(
            new Claim(RealtimeClaimTypes.World, worldId.ToString()),
            new Claim(RealtimeClaimTypes.PlayerActor, Guid.NewGuid().ToString()));
        var master = Principal(new Claim(RealtimeClaimTypes.GameMasterWorld, worldId.ToString()));

        Assert.True(PlayerWorldAuthorization.HasContext(authorized, worldId, playerId));
        Assert.True(PlayerWorldAuthorization.HasContext(master, worldId, playerId));
        Assert.False(PlayerWorldAuthorization.HasContext(wrongPlayer, worldId, playerId));
        Assert.False(PlayerWorldAuthorization.HasContext(new ClaimsPrincipal(), worldId, playerId));
    }

    private static ClaimsPrincipal Principal(params Claim[] claims) =>
        new(new ClaimsIdentity(claims, "test"));
}

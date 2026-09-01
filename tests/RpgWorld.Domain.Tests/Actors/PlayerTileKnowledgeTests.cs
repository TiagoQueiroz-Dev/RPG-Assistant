using RpgWorld.Domain.Actors;
using RpgWorld.Domain.Worlds;

namespace RpgWorld.Domain.Tests.Actors;

public sealed class PlayerTileKnowledgeTests
{
    [Fact]
    public void Observation_preserves_discovery_and_only_upgrades_historical_knowledge()
    {
        var playerId = Guid.NewGuid();
        var world = World.Create("Fog", 8, 8);
        var discovered = PlayerTileKnowledge.Discover(
            playerId, world.PositionAt(2, 3), known: false, DateTimeOffset.UnixEpoch);

        Assert.Equal(PlayerKnowledgeState.Discovered, discovered.CurrentState(visible: false));
        Assert.Equal(PlayerKnowledgeState.Visible, discovered.CurrentState(visible: true));

        discovered.Observe(known: true, DateTimeOffset.UnixEpoch.AddMinutes(1));
        discovered.Observe(known: false, DateTimeOffset.UnixEpoch.AddMinutes(2));

        Assert.Equal(PlayerKnowledgeState.Known, discovered.HistoricalState);
        Assert.Equal(PlayerKnowledgeState.Known, discovered.CurrentState(visible: false));
        Assert.NotNull(discovered.KnownAtUtc);
    }
}

using System.Text.Json;
using RpgWorld.Domain.Worlds.Events;

namespace RpgWorld.Domain.Tests.Worlds;

public sealed class WorldEventTests
{
    [Fact]
    public void Creates_versioned_event_with_position_actors_and_json_payload()
    {
        var worldId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var worldEvent = WorldEvent.Create(
            Guid.NewGuid(), worldId, "ActorKilled", DateTimeOffset.UnixEpoch,
            new WorldEventPosition(2, 3), [actorId, actorId], "{\"actorId\":\"safe-copy\"}", 2);

        Assert.Equal(worldId, worldEvent.WorldId);
        Assert.Equal(new WorldEventPosition(2, 3), worldEvent.Position);
        Assert.Equal(actorId, Assert.Single(worldEvent.ActorIds));
        Assert.Equal(2, worldEvent.PayloadVersion);
        Assert.Equal("safe-copy", JsonDocument.Parse(worldEvent.Payload).RootElement.GetProperty("actorId").GetString());
    }

    [Fact]
    public void Rejects_invalid_or_oversized_payloads()
    {
        Assert.ThrowsAny<JsonException>(() => WorldEvent.Create(
            Guid.NewGuid(), Guid.NewGuid(), "Invalid", DateTimeOffset.UnixEpoch, null, [], "not-json"));
        Assert.Throws<ArgumentException>(() => WorldEvent.Create(
            Guid.NewGuid(), Guid.NewGuid(), "TooLarge", DateTimeOffset.UnixEpoch, null, [],
            $"\"{new string('x', WorldEvent.MaximumPayloadLength)}\""));
    }
}

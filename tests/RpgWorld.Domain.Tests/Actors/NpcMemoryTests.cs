using RpgWorld.Domain.Actors.Memories;

namespace RpgWorld.Domain.Tests.Actors;

public sealed class NpcMemoryTests
{
    [Fact]
    public void Memory_normalizes_payload_and_reports_expiration()
    {
        var created = DateTimeOffset.UnixEpoch;
        var memory = NpcMemory.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            " Was-Attacked ",
            Guid.NewGuid(),
            45,
            created,
            created.AddDays(10),
            new Dictionary<string, string> { ["Damage"] = "25" });

        Assert.Equal(NpcMemoryEventTypes.WasAttacked, memory.EventType);
        Assert.Equal("25", memory.Payload["damage"]);
        Assert.False(memory.IsExpired(created.AddDays(9)));
        Assert.True(memory.IsExpired(created.AddDays(10)));
    }

    [Fact]
    public void Memory_rejects_invalid_importance_expiration_and_payload()
    {
        var actorId = Guid.NewGuid();
        var worldId = Guid.NewGuid();

        Assert.Throws<ArgumentOutOfRangeException>(() => NpcMemory.Create(
            actorId, worldId, "event", null, 0, DateTimeOffset.UnixEpoch));
        Assert.Throws<ArgumentOutOfRangeException>(() => NpcMemory.Create(
            actorId, worldId, "event", null, 10, DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch));
        Assert.Throws<ArgumentException>(() => NpcMemory.Create(
            actorId,
            worldId,
            "event",
            null,
            10,
            DateTimeOffset.UnixEpoch,
            payload: Enumerable.Range(0, 17).ToDictionary(index => $"key-{index}", _ => "value")));
    }
}

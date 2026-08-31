using RpgWorld.Domain.Actors;
using RpgWorld.Domain.Worlds;

namespace RpgWorld.Domain.Tests.Actors;

public sealed class ActorRelationshipTests
{
    [Fact]
    public void Relationships_are_directional_and_all_dimensions_are_bounded()
    {
        var now = DateTimeOffset.UnixEpoch;
        var world = World.Create("Directed relationships", 8, 8);
        var first = NpcActor.Create("First", world, world.PositionAt(1, 1), now);
        var second = NpcActor.Create("Second", world, world.PositionAt(2, 2), now);

        first.ApplyRelationship(second.Id, new ActorRelationshipModifier(
            "betrayal",
            friendship: -200,
            fear: 200,
            respect: -200,
            love: -200,
            hatred: 200,
            trust: -200), now);

        var relationship = Assert.Single(first.Relationships);
        Assert.Equal(-100, relationship.Friendship);
        Assert.Equal(100, relationship.Fear);
        Assert.Equal(-100, relationship.Respect);
        Assert.Equal(-100, relationship.Love);
        Assert.Equal(100, relationship.Hatred);
        Assert.Equal(-100, relationship.Trust);
        Assert.Empty(second.Relationships);
    }

    [Fact]
    public void Relationship_history_keeps_the_latest_relevant_changes()
    {
        var now = DateTimeOffset.UnixEpoch;
        var relationship = ActorRelationship.Neutral(Guid.NewGuid());

        for (var index = 0; index < 60; index++)
        {
            relationship = relationship.Apply(
                new ActorRelationshipModifier($"event-{index}", friendship: 1),
                now.AddMinutes(index));
        }

        Assert.Equal(50, relationship.History.Count);
        Assert.Equal("event-10", relationship.History[0].Reason);
        Assert.Equal("event-59", relationship.History[^1].Reason);
        Assert.Equal(60, relationship.Friendship);
    }
}

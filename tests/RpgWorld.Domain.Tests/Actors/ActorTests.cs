using RpgWorld.Domain.Actors;
using RpgWorld.Domain.Events;
using RpgWorld.Domain.Worlds;

namespace RpgWorld.Domain.Tests.Actors;

public sealed class ActorTests
{
    [Fact]
    public void Player_npc_and_creature_share_actor_state_and_creation_event()
    {
        var world = World.Create("Actors", 16, 16);
        var position = world.PositionAt(2, 3);
        var now = new DateTimeOffset(2026, 8, 30, 10, 0, 0, TimeSpan.Zero);
        Actor[] actors =
        [
            PlayerActor.Create("Ayla", world, position, now),
            NpcActor.Create("Merchant", world, position, now),
            CreatureActor.Create("Wolf", world, position, now, maximumHealth: 40)
        ];

        Assert.Equal(["player", "npc", "creature"], actors.Select(actor => actor.Kind));
        Assert.All(actors, actor =>
        {
            Assert.Equal(position, actor.Position);
            Assert.Equal(ActorStatus.Active, actor.Status);
            var created = Assert.IsType<ActorCreatedEvent>(Assert.Single(actor.DomainEvents));
            Assert.Equal(actor.Id, created.ActorId);
            Assert.Equal(actor.Kind, created.ActorKind);
        });
    }

    [Fact]
    public void Actor_rejects_position_outside_world()
    {
        var world = World.Create("Small", 4, 4);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            PlayerActor.Create(
                "Lost",
                world,
                new Position(world.Id, 4, 0),
                DateTimeOffset.UnixEpoch));
    }

    [Fact]
    public void Common_lifecycle_generates_move_damage_and_death_events()
    {
        var world = World.Create("Lifecycle", 8, 8);
        var actor = NpcActor.Create("Guard", world, world.PositionAt(1, 1), DateTimeOffset.UnixEpoch, 50);
        actor.ClearDomainEvents();
        var attackerId = Guid.NewGuid();

        actor.Move(world, world.PositionAt(2, 1), DateTimeOffset.UnixEpoch.AddMinutes(1));
        actor.SetCurrentAction("defend", DateTimeOffset.UnixEpoch.AddMinutes(2));
        actor.TakeDamage(75, attackerId, DateTimeOffset.UnixEpoch.AddMinutes(3));

        Assert.Equal(world.PositionAt(2, 1), actor.Position);
        Assert.Equal(0, actor.Health);
        Assert.Equal(ActorStatus.Dead, actor.Status);
        Assert.Null(actor.CurrentAction);
        Assert.Collection(
            actor.DomainEvents.Where(value => value is not NpcActionExecutionChangedEvent),
            domainEvent => Assert.IsType<ActorMovedEvent>(domainEvent),
            domainEvent => Assert.IsType<ActorDamagedEvent>(domainEvent),
            domainEvent => Assert.IsType<ActorKilledEvent>(domainEvent));
        Assert.Throws<InvalidOperationException>(() =>
            actor.Move(world, world.PositionAt(3, 1), DateTimeOffset.UnixEpoch.AddMinutes(4)));
    }

    [Fact]
    public void Actor_manages_attributes_inventory_faction_reputation_and_relationships()
    {
        var world = World.Create("State", 8, 8);
        var actor = CreatureActor.Create("Companion", world, world.PositionAt(0, 0), DateTimeOffset.UnixEpoch);
        var factionId = Guid.NewGuid();
        var friendId = Guid.NewGuid();

        actor.SetAttribute("strength", 14, DateTimeOffset.UnixEpoch);
        actor.AddInventory("ration", 2, DateTimeOffset.UnixEpoch);
        actor.AddInventory("RATION", 3, DateTimeOffset.UnixEpoch);
        actor.JoinFaction(factionId, DateTimeOffset.UnixEpoch);
        actor.SetReputation(factionId, 25, DateTimeOffset.UnixEpoch);
        actor.SetRelationship(friendId, "friend", 80, DateTimeOffset.UnixEpoch);

        Assert.Equal(14, actor.Attributes["STRENGTH"]);
        Assert.Equal(5, Assert.Single(actor.Inventory).Quantity);
        Assert.Equal(factionId, actor.FactionId);
        Assert.Equal(25, actor.Reputation[factionId]);
        Assert.Equal(friendId, Assert.Single(actor.Relationships).ActorId);
    }
}

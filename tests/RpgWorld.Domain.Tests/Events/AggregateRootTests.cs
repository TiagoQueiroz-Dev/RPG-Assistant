using RpgWorld.Domain.Events;

namespace RpgWorld.Domain.Tests.Events;

public sealed class AggregateRootTests
{
    [Fact]
    public void Domain_operation_records_event_until_publication()
    {
        var actorId = Guid.NewGuid();
        var worldId = Guid.NewGuid();
        var actor = new TestActor(actorId, worldId);

        Assert.Empty(actor.DomainEvents);

        actor.Kill();

        var killed = Assert.IsType<ActorKilledEvent>(Assert.Single(actor.DomainEvents));
        Assert.Equal(actorId, killed.ActorId);
        Assert.Equal(worldId, killed.WorldId);
        Assert.NotEqual(Guid.Empty, killed.EventId);

        actor.ClearDomainEvents();
        Assert.Empty(actor.DomainEvents);
    }

    private sealed class TestActor(Guid id, Guid worldId) : AggregateRoot
    {
        public void Kill() => RaiseDomainEvent(new ActorKilledEvent(
            id,
            killerId: null,
            worldId,
            DateTimeOffset.UtcNow));
    }
}


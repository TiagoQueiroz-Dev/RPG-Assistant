using RpgWorld.Application.Actors;
using RpgWorld.Application.Actors.Relationships;
using RpgWorld.Domain.Actors;
using RpgWorld.Domain.Worlds;

namespace RpgWorld.Application.Tests.Actors;

public sealed class ActorRelationshipServiceTests
{
    [Fact]
    public async Task Service_applies_multi_dimension_event_and_saves_source_only()
    {
        var now = DateTimeOffset.UnixEpoch;
        var world = World.Create("Relationship service", 8, 8);
        var source = NpcActor.Create("Source", world, world.PositionAt(1, 1), now);
        var target = PlayerActor.Create("Target", world, world.PositionAt(2, 2), now);
        var repository = new FakeActorRepository(source, target);
        var service = new ActorRelationshipService(repository);

        var relationship = await service.ApplyAsync(new ActorRelationshipChangeRequest(
            source.Id,
            target.Id,
            new ActorRelationshipModifier("player-rescue", friendship: 30, respect: 40, trust: 50),
            now.AddHours(1)));

        Assert.Equal(30, relationship.Friendship);
        Assert.Equal(40, relationship.Respect);
        Assert.Equal(50, relationship.Trust);
        Assert.Equal("player-rescue", Assert.Single(relationship.History).Reason);
        Assert.Empty(target.Relationships);
        Assert.Equal(1, repository.SaveCalls);
    }

    private sealed class FakeActorRepository(params Actor[] actors) : IActorRepository
    {
        public int SaveCalls { get; private set; }
        public Task<Actor?> GetAsync(Guid actorId, CancellationToken cancellationToken = default) => Task.FromResult(actors.SingleOrDefault(actor => actor.Id == actorId));
        public Task<IReadOnlyList<Actor>> ListByWorldAsync(Guid worldId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<Actor>> ListAtPositionAsync(Position position, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public void Add(Actor actor) => throw new NotSupportedException();
        public Task SaveChangesAsync(CancellationToken cancellationToken = default) { SaveCalls++; return Task.CompletedTask; }
    }
}

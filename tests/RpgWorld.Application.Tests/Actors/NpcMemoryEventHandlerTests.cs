using RpgWorld.Application.Actors;
using RpgWorld.Application.Actors.Memories;
using RpgWorld.Domain.Actors;
using RpgWorld.Domain.Actors.Memories;
using RpgWorld.Domain.Events;
using RpgWorld.Domain.Worlds;

namespace RpgWorld.Application.Tests.Actors;

public sealed class NpcMemoryEventHandlerTests
{
    [Fact]
    public async Task Damage_event_creates_expiring_memory_and_changes_relationship()
    {
        var now = DateTimeOffset.UnixEpoch;
        var world = World.Create("Damage memory", 8, 8);
        var npc = NpcActor.Create("Victim", world, world.PositionAt(1, 1), now);
        var attackerId = Guid.NewGuid();
        var repository = new FakeActorRepository(npc);
        var memories = new FakeMemoryRepository();
        var recorder = new NpcMemoryEventRecorder(repository, memories, new NpcMemoryOptions());

        await recorder.RecordAsync(new ActorDamagedEvent(
            npc.Id, attackerId, world.Id, 25, 75, now.AddHours(1)));

        var memory = Assert.Single(memories.Items);
        Assert.Equal(NpcMemoryEventTypes.WasAttacked, memory.EventType);
        Assert.Equal(45, memory.Importance);
        Assert.NotNull(memory.ExpiresAt);
        Assert.Equal(-45, npc.Relationships.Single(relationship => relationship.ActorId == attackerId).Affinity);
        Assert.Equal(1, memories.SaveCalls);
    }

    [Fact]
    public async Task Killing_family_member_creates_permanent_memory_for_each_living_relative()
    {
        var now = DateTimeOffset.UnixEpoch;
        var world = World.Create("Family memory", 8, 8);
        var victim = PlayerActor.Create("Victim", world, world.PositionAt(1, 1), now);
        var relative = NpcActor.Create("Relative", world, world.PositionAt(2, 2), now);
        relative.AddFamilyMember(victim.Id, now);
        var killerId = Guid.NewGuid();
        var memories = new FakeMemoryRepository();
        var recorder = new NpcMemoryEventRecorder(
            new FakeActorRepository(victim, relative),
            memories,
            new NpcMemoryOptions());

        await recorder.RecordAsync(new ActorKilledEvent(victim.Id, killerId, world.Id, now.AddHours(1)));

        var memory = Assert.Single(memories.Items);
        Assert.Equal(relative.Id, memory.ActorId);
        Assert.Equal(NpcMemoryEventTypes.FamilyMemberKilled, memory.EventType);
        Assert.Equal(100, memory.Importance);
        Assert.Null(memory.ExpiresAt);
        Assert.Equal(-100, relative.Relationships.Single(relationship => relationship.ActorId == killerId).Affinity);
    }

    private sealed class FakeActorRepository(params Actor[] actors) : IActorRepository
    {
        public Task<Actor?> GetAsync(Guid actorId, CancellationToken cancellationToken = default) => Task.FromResult(actors.SingleOrDefault(actor => actor.Id == actorId));
        public Task<IReadOnlyList<Actor>> ListByWorldAsync(Guid worldId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<Actor>>(actors.Where(actor => actor.WorldId == worldId).ToArray());
        public Task<IReadOnlyList<Actor>> ListAtPositionAsync(Position position, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public void Add(Actor actor) => throw new NotSupportedException();
        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeMemoryRepository : INpcMemoryRepository
    {
        public List<NpcMemory> Items { get; } = [];
        public int SaveCalls { get; private set; }
        public void Add(NpcMemory memory) => Items.Add(memory);
        public Task<IReadOnlyList<NpcMemory>> ListAsync(Guid actorId, Guid? targetId, DateTimeOffset asOf, int minimumImportance = 1, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<NpcMemory>> ListRelevantForActorsAsync(IReadOnlyCollection<Guid> actorIds, DateTimeOffset asOf, int minimumImportance, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<int> DeleteExpiredAsync(DateTimeOffset asOf, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task SaveChangesAsync(CancellationToken cancellationToken = default) { SaveCalls++; return Task.CompletedTask; }
    }
}

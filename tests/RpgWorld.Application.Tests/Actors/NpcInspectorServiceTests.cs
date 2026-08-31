using RpgWorld.Application.Actors;
using RpgWorld.Application.Actors.Inspection;
using RpgWorld.Domain.Actors;
using RpgWorld.Domain.Actors.Traits;
using RpgWorld.Domain.Worlds;

namespace RpgWorld.Application.Tests.Actors;

public sealed class NpcInspectorServiceTests
{
    [Fact]
    public async Task Inspector_lists_tile_actors_and_resolves_available_and_missing_traits()
    {
        var now = DateTimeOffset.UnixEpoch;
        var world = World.Create("Inspector", 8, 8);
        var npc = NpcActor.Create("NPC", world, world.PositionAt(2, 3), now);
        var player = PlayerActor.Create("Player", world, world.PositionAt(2, 3), now);
        var available = Trait("available", 1.25m);
        var removedModuleTrait = Trait("removed-module", 1.1m);
        npc.AddTrait(available, now);
        npc.AddTrait(removedModuleTrait, now);
        var repository = new FakeActorRepository(npc, player);
        var service = new NpcInspectorService(
            repository,
            new TraitDefinitionCatalog([available]));

        var actors = await service.ListAtPositionAsync(world.Id, 2, 3);
        var inspector = Assert.IsType<NpcInspectorView>(await service.GetNpcAsync(npc.Id));

        Assert.Equal(2, actors.Count);
        Assert.Equal(["available", "removed-module"], actors.Single(actor => actor.ActorId == npc.Id).TraitCodes);
        Assert.True(inspector.Traits.Single(trait => trait.Code == "available").DefinitionAvailable);
        Assert.False(inspector.Traits.Single(trait => trait.Code == "removed-module").DefinitionAvailable);
        Assert.Equal(1.25m, inspector.Traits[0].ActionScoreMultipliers["Work"]);
        Assert.Null(await service.GetNpcAsync(player.Id));
    }

    private static TraitDefinition Trait(string code, decimal multiplier) =>
        new(code, code, $"{code} trait.", new Dictionary<string, decimal> { ["Work"] = multiplier });

    private sealed class FakeActorRepository(params Actor[] actors) : IActorRepository
    {
        public Task<Actor?> GetAsync(Guid actorId, CancellationToken cancellationToken = default) =>
            Task.FromResult(actors.SingleOrDefault(actor => actor.Id == actorId));

        public Task<IReadOnlyList<Actor>> ListByWorldAsync(Guid worldId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Actor>>(actors.Where(actor => actor.WorldId == worldId).ToArray());

        public Task<IReadOnlyList<Actor>> ListAtPositionAsync(Position position, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Actor>>(actors.Where(actor => actor.Position == position).ToArray());

        public void Add(Actor actor) => throw new NotSupportedException();
        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}

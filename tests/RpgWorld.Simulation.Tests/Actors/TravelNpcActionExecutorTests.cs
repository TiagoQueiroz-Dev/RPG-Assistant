using RpgWorld.Application.Actors.Actions;
using RpgWorld.Application.Actors.Movement;
using RpgWorld.Domain.Actors;
using RpgWorld.Domain.Actors.Actions;
using RpgWorld.Domain.Worlds;
using RpgWorld.Simulation.Actors;
using RpgWorld.Simulation.Actors.Actions;

namespace RpgWorld.Simulation.Tests.Actors;

public sealed class TravelNpcActionExecutorTests
{
    [Theory]
    [InlineData(ActorPathStatus.NoPath, NpcActionStepOutcome.Fail, 1)]
    [InlineData(ActorPathStatus.SearchLimitReached, NpcActionStepOutcome.Continue, 2)]
    public async Task Unreachable_paths_fail_but_search_limits_do_not_end_the_action(
        ActorPathStatus status, NpcActionStepOutcome expected, int searches)
    {
        var world = World.Create("Travel", 8, 8);
        var npc = NpcActor.Create("Walker", world, world.PositionAt(0, 0), DateTimeOffset.UnixEpoch);
        npc.SelectAction("Travel", DateTimeOffset.UnixEpoch);
        var target = new NpcActionTarget(world.PositionAt(7, 7));
        var paths = new PathStub(status);
        var executor = new TravelNpcActionExecutor(new DestinationStub(target), paths, new NoMovement());
        var result = await executor.ExecuteAsync(new(npc, npc.ActionExecution!, DateTimeOffset.UnixEpoch));
        Assert.Equal(expected, result.Outcome);
        Assert.Equal(searches, paths.Calls);
        Assert.Equal(target, npc.ActionExecution!.Target);
        Assert.Equal(world.PositionAt(0, 0), npc.Position);
    }

    [Fact]
    public async Task Missing_destination_is_an_explicit_failure()
    {
        var world = World.Create("Travel", 8, 8);
        var npc = NpcActor.Create("Walker", world, world.PositionAt(0, 0), DateTimeOffset.UnixEpoch);
        npc.SelectAction("Travel", DateTimeOffset.UnixEpoch);
        var paths = new PathStub(ActorPathStatus.Found);
        var executor = new TravelNpcActionExecutor(new DestinationStub(null), paths, new NoMovement());
        var result = await executor.ExecuteAsync(new(npc, npc.ActionExecution!, DateTimeOffset.UnixEpoch));
        Assert.Equal(NpcActionStepOutcome.Fail, result.Outcome);
        Assert.Contains("destination", result.Reason);
        Assert.Equal(0, paths.Calls);
    }

    private sealed class DestinationStub(NpcActionTarget? target) : INpcTravelDestinationResolver
    {
        public Task<NpcActionTarget?> ResolveAsync(NpcActor npc, CancellationToken cancellationToken = default) => Task.FromResult(target);
    }

    private sealed class PathStub(ActorPathStatus status) : IActorPathfinder
    {
        public int Calls { get; private set; }
        public Task<ActorPathResult> FindAsync(Actor actor, Position destination, PathfindingOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.FromResult(new ActorPathResult(status, [], 0m, 0, "Route unavailable."));
        }
    }

    private sealed class NoMovement : ISimulationActorMovementService
    {
        public Task<ActorMoveResult> MoveDuringTickAsync(ActorMoveRequest request, Guid worldId, DateTimeOffset instant,
            CancellationToken cancellationToken = default) => throw new InvalidOperationException("No movement should occur.");
    }
}

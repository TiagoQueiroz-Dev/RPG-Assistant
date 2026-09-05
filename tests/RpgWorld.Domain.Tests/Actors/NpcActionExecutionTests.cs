using RpgWorld.Domain.Actors;
using RpgWorld.Domain.Actors.Actions;
using RpgWorld.Domain.Events;
using RpgWorld.Domain.Worlds;

namespace RpgWorld.Domain.Tests.Actors;

public sealed class NpcActionExecutionTests
{
    private static readonly DateTimeOffset Start = DateTimeOffset.UnixEpoch;

    [Fact]
    public void Decision_retains_progress_and_target_until_explicit_completion()
    {
        var world = World.Create("Actions", 16, 16);
        var npc = NpcActor.Create("Walker", world, world.PositionAt(1, 1), Start);
        var target = new NpcActionTarget(world.PositionAt(7, 9));
        Assert.True(npc.SelectAction("Travel", Start, target));
        var id = npc.ActionExecution!.Id;
        npc.AdvanceAction(id, 0.25m, Start.AddMinutes(1));
        Assert.False(npc.SelectAction("Travel", Start.AddMinutes(2)));
        Assert.Equal((id, 0.25m, target), (npc.ActionExecution.Id, npc.ActionExecution.Progress, npc.ActionExecution.Target));
        Assert.False(npc.ActionExecution.CanProcess(Start.AddMinutes(1)));
        Assert.True(npc.ActionExecution.CanProcess(Start.AddMinutes(2)));
        var eventCount = npc.DomainEvents.Count;
        npc.AdvanceAction(id, 0.5m, Start.AddMinutes(1));
        Assert.Equal(eventCount, npc.DomainEvents.Count);
        Assert.Equal(0.25m, npc.ActionExecution.Progress);
        npc.AdvanceAction(id, 0.5m, Start.AddMinutes(2));
        npc.FinishAction(id, NpcActionStatus.Completed, Start.AddMinutes(3));
        Assert.Equal((NpcActionStatus.Completed, 1m), (npc.ActionExecution.Status, npc.ActionExecution.Progress));
        Assert.Null(npc.CurrentAction);
        Assert.Throws<InvalidOperationException>(() => npc.AdvanceAction(id, 1m, Start.AddMinutes(4)));
        Assert.True(npc.SelectAction("Travel", Start.AddMinutes(4), target));
        Assert.NotEqual(id, npc.ActionExecution.Id);
    }

    [Fact]
    public void Replacement_policy_cancels_previous_execution_and_rejects_stale_results()
    {
        var world = World.Create("Actions", 16, 16);
        var npc = NpcActor.Create("Worker", world, world.PositionAt(1, 1), Start);
        npc.SelectAction("Work", Start);
        var workId = npc.ActionExecution!.Id;
        Assert.False(npc.SelectAction("Sleep", Start.AddMinutes(1), policy: NpcActionReplacementPolicy.KeepRunning));
        npc.SelectAction("Sleep", Start.AddMinutes(2));
        Assert.Contains(npc.DomainEvents.OfType<NpcActionExecutionChangedEvent>(), value =>
            value.Execution.Id == workId && value.Execution.Status == NpcActionStatus.Cancelled);
        Assert.Throws<InvalidOperationException>(() => npc.FinishAction(workId, NpcActionStatus.Completed, Start.AddMinutes(3)));
        var sleepId = npc.ActionExecution.Id;
        npc.SelectAction("Sleep", Start.AddMinutes(3), policy: NpcActionReplacementPolicy.Restart);
        Assert.NotEqual(sleepId, npc.ActionExecution.Id);
        npc.SelectAction(null, Start.AddMinutes(4));
        Assert.Equal(NpcActionStatus.Cancelled, npc.ActionExecution.Status);
        Assert.Null(npc.CurrentAction);
    }

    [Theory]
    [InlineData(NpcActionTargetKind.Actor)]
    [InlineData(NpcActionTargetKind.Structure)]
    [InlineData(NpcActionTargetKind.WorldEntity)]
    public void Entity_targets_and_failure_details_are_retained(NpcActionTargetKind kind)
    {
        var world = World.Create("Actions", 16, 16);
        var npc = NpcActor.Create("Worker", world, world.PositionAt(1, 1), Start);
        npc.SelectAction("Work", Start);
        var target = new NpcActionTarget(world.PositionAt(3, 5), kind, Guid.NewGuid());
        npc.SetActionTarget(npc.ActionExecution!.Id, target, Start);
        npc.FinishAction(npc.ActionExecution.Id, NpcActionStatus.Failed, Start.AddMinutes(1), "Destination unavailable.");
        Assert.Equal(target, npc.ActionExecution.Target);
        Assert.Equal("Destination unavailable.", npc.ActionExecution.Reason);
        Assert.Equal(NpcActionStatus.Failed, npc.ActionExecution.Status);
        Assert.Null(npc.CurrentAction);
    }

    [Fact]
    public void Legacy_action_api_and_damage_keep_execution_state_consistent()
    {
        var world = World.Create("Actions", 16, 16);
        var npc = NpcActor.Create("Worker", world, world.PositionAt(1, 1), Start);
        Actor actor = npc;
        actor.SetCurrentAction("Work", Start);
        Assert.Equal("Work", npc.ActionExecution!.ActionCode);
        actor.TakeDamage(1, null, Start.AddMinutes(1));
        Assert.Equal(NpcActionStatus.Cancelled, npc.ActionExecution.Status);
        Assert.Null(npc.CurrentAction);
        npc.SelectAction("Sleep", Start.AddMinutes(2));
        actor.TakeDamage(100, null, Start.AddMinutes(3));
        Assert.Equal("Actor died.", npc.ActionExecution.Reason);
        Assert.Null(npc.CurrentAction);
    }

    [Fact]
    public void Invalid_replacement_and_progress_leave_running_action_unchanged()
    {
        var world = World.Create("Actions", 16, 16);
        var npc = NpcActor.Create("Worker", world, world.PositionAt(1, 1), Start);
        npc.SelectAction("Work", Start);
        var original = npc.ActionExecution!;
        Assert.Throws<ArgumentException>(() => npc.SelectAction(new string('x', 121), Start));
        Assert.Throws<ArgumentException>(() => npc.SetActionTarget(original.Id,
            new NpcActionTarget(new Position(Guid.NewGuid(), 1, 1)), Start));
        Assert.Throws<ArgumentOutOfRangeException>(() => npc.AdvanceAction(original.Id, 1.1m, Start));
        Assert.Throws<ArgumentOutOfRangeException>(() => npc.AdvanceAction(original.Id, 0.1m, Start.AddSeconds(-1)));
        Assert.Throws<ArgumentException>(() => npc.FinishAction(original.Id, NpcActionStatus.Failed, Start));
        Assert.Equal(original, npc.ActionExecution);
        Assert.Equal("Work", npc.CurrentAction);
    }
}

using RpgWorld.Application.Actors.Movement;
using RpgWorld.Domain.Actors.Actions;
using RpgWorld.Domain.Worlds;

namespace RpgWorld.Simulation.Actors.Actions;

public sealed class NpcActionNavigation(IActorPathfinder paths, ISimulationActorMovementService movement)
{
    // Null means already at the activity site. Moving consumes this tick without applying the activity effect.
    public async Task<NpcActionStepResult?> ApproachAsync(NpcActionExecutionContext context, NpcActionTarget target,
        CancellationToken cancellationToken)
    {
        var destination = target.Position ?? throw new ArgumentException("A destination position is required.");
        if (context.Actor.ActionExecution!.Target != target)
            context.Actor.SetActionTarget(context.Execution.Id, target, context.Instant);
        if (context.Actor.Position == destination) return null;
        var route = await paths.FindAsync(context.Actor, destination, cancellationToken: cancellationToken);
        if (route.Status == ActorPathStatus.NoPath) return new(NpcActionStepOutcome.Fail, Reason: route.Reason);
        if (route.Status == ActorPathStatus.SearchLimitReached)
            return new(NpcActionStepOutcome.Continue, context.Execution.Progress, route.Reason);
        var next = route.Steps[0];
        await movement.MoveDuringTickAsync(new(context.Actor.Id, next.X, next.Y), context.Actor.WorldId, context.Instant, cancellationToken);
        return new(NpcActionStepOutcome.Continue, context.Execution.Progress);
    }
}

using RpgWorld.Application.Actors.Actions;
using RpgWorld.Application.Actors.Movement;
using RpgWorld.Simulation.Actors.Utility;

namespace RpgWorld.Simulation.Actors.Actions;

public sealed class TravelNpcActionExecutor(INpcTravelDestinationResolver destinations, IActorPathfinder pathfinder,
    ISimulationActorMovementService movement) : INpcActionExecutor
{
    public string ActionCode => NpcActionCodes.Travel;

    public async Task<NpcActionStepResult> ExecuteAsync(NpcActionExecutionContext context, CancellationToken cancellationToken = default)
    {
        var target = await destinations.ResolveAsync(context.Actor, cancellationToken);
        if (target?.Position is not { } destination)
            return new(NpcActionStepOutcome.Fail, Reason: "No valid travel destination was found.");
        if (context.Actor.ActionExecution?.Target != target)
            context.Actor.SetActionTarget(context.Execution.Id, target, context.Instant);
        if (context.Actor.Position == destination) return Complete(context);

        // Rebuild from saved target and authoritative position after restarts or terrain changes.
        var route = await pathfinder.FindAsync(context.Actor, destination, cancellationToken: cancellationToken);
        if (route.Status == ActorPathStatus.SearchLimitReached)
            route = await pathfinder.FindAsync(context.Actor, destination,
                new(MaximumExpandedNodes: 65_536, MaximumLoadedTiles: 262_144, SearchPadding: 128), cancellationToken);
        if (route.Status == ActorPathStatus.SearchLimitReached)
            return new(NpcActionStepOutcome.Continue, context.Execution.Progress, route.Reason);
        if (route.Status == ActorPathStatus.NoPath)
            return new(NpcActionStepOutcome.Fail, Reason: route.Reason);
        var next = route.Steps[0];
        await movement.MoveDuringTickAsync(new(context.Actor.Id, next.X, next.Y), context.Actor.WorldId, context.Instant, cancellationToken);
        return context.Actor.Position == destination ? Complete(context)
            : new(NpcActionStepOutcome.Continue,
                context.Execution.Progress + (1m - context.Execution.Progress) / route.Steps.Count);
    }

    private static NpcActionStepResult Complete(NpcActionExecutionContext context)
    {
        context.Actor.RemoveGoal("travel", context.Instant);
        return new(NpcActionStepOutcome.Complete, 1m);
    }
}

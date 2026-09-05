using RpgWorld.Application.Actors.Actions;
using RpgWorld.Domain.Actors.Actions;
using RpgWorld.Domain.Worlds.Resources;
using RpgWorld.Simulation.Actors.Utility;

namespace RpgWorld.Simulation.Actors.Actions;

public sealed class EatNpcActionExecutor(INpcDailyActivityStore store, NpcActionNavigation navigation, UtilityAiOptions options) : INpcActionExecutor
{
    public string ActionCode => NpcActionCodes.Eat;
    public async Task<NpcActionStepResult> ExecuteAsync(NpcActionExecutionContext context, CancellationToken cancellationToken = default)
    {
        var item = context.Actor.Inventory.FirstOrDefault(item => options.FoodItemCodes.Contains(item.ItemCode) && item.Quantity > 0);
        if (item is not null) context.Actor.ConsumeInventory(item.ItemCode, 1, context.Instant);
        else
        {
            var food = await store.FindFoodAsync(context.Actor, cancellationToken);
            if (food is null) return new(NpcActionStepOutcome.Fail, Reason: "No food source is available.");
            var approach = await navigation.ApproachAsync(context,
                new(food.Position, NpcActionTargetKind.WorldEntity, food.Deposit.Id), cancellationToken);
            if (approach is not null) return approach;
            var extracted = food.Deposit.Extract(1m, ResourceConsumer.Actor(context.Actor.Id), context.Instant);
            if (extracted.Quantity != 1m) return new(NpcActionStepOutcome.Fail, Reason: "Food source was depleted.");
        }
        context.Actor.Eat(35m, context.Instant);
        return new(NpcActionStepOutcome.Complete, 1m);
    }
}

public sealed class SleepNpcActionExecutor(INpcDailyActivityStore store, NpcActionNavigation navigation,
    UtilityAiOptions options) : INpcActionExecutor
{
    public string ActionCode => NpcActionCodes.Sleep;
    public async Task<NpcActionStepResult> ExecuteAsync(NpcActionExecutionContext context, CancellationToken cancellationToken = default)
    {
        var safety = new DefaultNpcDecisionContextProvider(options).Create(context.Actor).Safety;
        if (safety < options.MinimumSafetyForSleep) return new(NpcActionStepOutcome.Cancel, Reason: "Resting is unsafe.");
        var location = context.Actor.Home ?? context.Actor.Position;
        if (!await store.CanRestAsync(context.Actor, location, cancellationToken))
            return new(NpcActionStepOutcome.Fail, Reason: "No valid resting location.");
        var approach = await navigation.ApproachAsync(context, new(location), cancellationToken);
        if (approach is not null) return approach;
        var hours = (decimal)context.Elapsed.TotalHours;
        if (hours > 0m) context.Actor.Rest(hours * 25m, context.Instant);
        return context.Actor.Energy >= 99m ? new(NpcActionStepOutcome.Complete, 1m)
            : new(NpcActionStepOutcome.Continue, Math.Max(context.Execution.Progress, context.Actor.Energy / 100m));
    }
}

public sealed class WorkNpcActionExecutor(INpcDailyActivityStore store, NpcActionNavigation navigation,
    UtilityAiOptions options) : INpcActionExecutor
{
    public string ActionCode => NpcActionCodes.Work;
    private static readonly IReadOnlyDictionary<string, string> Production = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        { ["farmer"] = "food", ["lumberjack"] = "wood", ["miner"] = "stone", ["artisan"] = "tools" };
    private static readonly HashSet<string> Services = new(StringComparer.OrdinalIgnoreCase)
        { "merchant", "guard", "healer", "scholar", "laborer", "innkeeper" };

    public async Task<NpcActionStepResult> ExecuteAsync(NpcActionExecutionContext context, CancellationToken cancellationToken = default)
    {
        var npc = context.Actor;
        if (npc.Job is null || (!Production.ContainsKey(npc.Job) && !Services.Contains(npc.Job)))
            return new(NpcActionStepOutcome.Fail, Reason: "No supported job is assigned.");
        if (npc.Energy < options.MinimumEnergyForWork || new DefaultNpcDecisionContextProvider(options).Create(npc).Safety < options.MinimumSafetyForWork)
            return new(NpcActionStepOutcome.Cancel, Reason: "NPC cannot safely work.");
        var city = await store.GetWorkCityAsync(npc, cancellationToken);
        if (city is null) return new(NpcActionStepOutcome.Fail, Reason: "No active workplace city.");
        var approach = await navigation.ApproachAsync(context, new(city.Center, NpcActionTargetKind.WorldEntity, city.Id), cancellationToken);
        if (approach is not null) return approach;
        var progress = Math.Min(1m, context.Execution.Progress + (decimal)context.Elapsed.TotalHours);
        if (progress < 1m) return new(NpcActionStepOutcome.Continue, progress);
        if (city.Wealth < 2m) return new(NpcActionStepOutcome.Fail, Reason: "Workplace cannot pay the wage.");
        if (Production.TryGetValue(npc.Job, out var resource)) city.StoreResource(resource, 1m, context.Instant);
        city.DebitWealth(2m, context.Instant);
        npc.Earn(2m, context.Instant);
        npc.ConsumeEnergy(5m, context.Instant);
        return new(NpcActionStepOutcome.Complete, 1m);
    }
}

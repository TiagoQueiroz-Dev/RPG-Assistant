using RpgWorld.Domain.Actors;
using RpgWorld.Domain.Worlds;
using RpgWorld.Simulation.Actors.Utility;

namespace RpgWorld.Simulation.Tests.Actors;

public sealed class NpcUtilityDecisionServiceTests
{
    [Fact]
    public void Hungry_npc_with_food_reproducibly_selects_eat_and_explains_factors()
    {
        var (npc, start) = CreateNpc();
        npc.AdvanceNeedsTo(start.AddHours(10));
        var context = new NpcDecisionContext(npc, 1m, 1m, 0m, false, 0m);
        var service = CreateService(new UtilityAiOptions());

        var first = Assert.IsType<NpcDecision>(service.Decide(context));
        var second = Assert.IsType<NpcDecision>(service.Decide(context));

        Assert.Equal(NpcActionCodes.Eat, first.ActionCode);
        Assert.Equal(first.ActionCode, second.ActionCode);
        Assert.Equal(first.Score, second.Score);
        Assert.Equal(first.Explain(), second.Explain());
        Assert.Contains("Hunger=0.4000", first.Explain(), StringComparison.Ordinal);
        Assert.Equal(5, first.Candidates.Count);
    }

    [Fact]
    public void Fatigued_npc_selects_sleep_when_eating_is_ineligible()
    {
        var (npc, start) = CreateNpc();
        npc.AdvanceNeedsTo(start.AddHours(1), hungerPerWorldHour: 0m, energyPerWorldHour: 90m);
        var service = CreateService(new UtilityAiOptions());

        var decision = Assert.IsType<NpcDecision>(service.Decide(
            new NpcDecisionContext(npc, 0m, 1m, 0m, false, 0m)));
        var eat = decision.Candidates.Single(candidate => candidate.ActionCode == NpcActionCodes.Eat);

        Assert.Equal(NpcActionCodes.Sleep, decision.ActionCode);
        Assert.False(eat.IsEligible);
        Assert.Equal("NPC is not hungry.", eat.IneligibilityReason);
    }

    [Fact]
    public void Employed_npc_without_money_selects_work()
    {
        var (npc, start) = CreateNpc();
        npc.AssignJob("farmer", start);
        var service = CreateService(new UtilityAiOptions());

        var decision = Assert.IsType<NpcDecision>(service.Decide(
            new NpcDecisionContext(npc, 0m, 1m, 0m, false, 0m)));

        Assert.Equal(NpcActionCodes.Work, decision.ActionCode);
        Assert.Equal(1m, decision.Score);
    }

    [Fact]
    public void Eligibility_excludes_unavailable_actions_and_allows_travel_and_attack()
    {
        var (traveler, _) = CreateNpc();
        var service = CreateService(new UtilityAiOptions());

        var travelDecision = Assert.IsType<NpcDecision>(service.Decide(
            new NpcDecisionContext(traveler, 0m, 1m, 1m, false, 0m)));
        var attackDecision = Assert.IsType<NpcDecision>(service.Decide(
            new NpcDecisionContext(traveler, 0m, 0m, 0m, true, 1m)));

        Assert.Equal(NpcActionCodes.Travel, travelDecision.ActionCode);
        Assert.Equal(NpcActionCodes.AttackEnemy, attackDecision.ActionCode);
        Assert.False(attackDecision.Candidates.Single(candidate =>
            candidate.ActionCode == NpcActionCodes.Sleep).IsEligible);
    }

    [Fact]
    public void Changing_weights_changes_the_decision_without_changing_the_evaluator()
    {
        var (npc, start) = CreateNpc();
        npc.AdvanceNeedsTo(start.AddHours(1), hungerPerWorldHour: 70m, energyPerWorldHour: 60m);
        var context = new NpcDecisionContext(npc, 0.2m, 1m, 0m, false, 0m);
        var defaults = CreateService(new UtilityAiOptions());
        var customOptions = new UtilityAiOptions();
        customOptions.ActionWeights[NpcActionCodes.Eat]["Hunger"] = 1m;
        customOptions.ActionWeights[NpcActionCodes.Eat]["FoodAvailability"] = 0m;
        customOptions.ActionWeights[NpcActionCodes.Sleep]["Fatigue"] = 1m;
        customOptions.ActionWeights[NpcActionCodes.Sleep]["Safety"] = 0m;
        var customized = CreateService(customOptions);

        Assert.Equal(NpcActionCodes.Sleep, defaults.Decide(context)?.ActionCode);
        Assert.Equal(NpcActionCodes.Eat, customized.Decide(context)?.ActionCode);
    }

    [Fact]
    public void Default_context_uses_inventory_home_goals_and_hostile_relationships()
    {
        var (npc, start) = CreateNpc();
        npc.AddInventory("ration", 2, start);
        npc.SetGoal("explore", 50, null, start);
        npc.SetRelationship(Guid.NewGuid(), "enemy", -75, start);
        var provider = new DefaultNpcDecisionContextProvider(new UtilityAiOptions());

        var context = provider.Create(npc);

        Assert.Equal(2m / 3m, context.FoodAvailability);
        Assert.Equal(0.25m, context.Safety);
        Assert.Equal(1m, context.TravelOpportunity);
        Assert.True(context.EnemyPresent);
        Assert.Equal(0.75m, context.EnemyThreat);
    }

    private static INpcUtilityDecisionService CreateService(UtilityAiOptions options) =>
        new NpcUtilityDecisionService(
            [
                new EatNpcAction(),
                new SleepNpcAction(options),
                new WorkNpcAction(options),
                new TravelNpcAction(options),
                new AttackEnemyNpcAction(options)
            ],
            options);

    private static (NpcActor Npc, DateTimeOffset Start) CreateNpc()
    {
        var start = new DateTimeOffset(2026, 8, 31, 6, 0, 0, TimeSpan.Zero);
        var world = World.Create("Utility AI", 8, 8);
        return (NpcActor.Create("Villager", world, world.PositionAt(1, 1), start), start);
    }
}

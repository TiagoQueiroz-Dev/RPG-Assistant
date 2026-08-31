namespace RpgWorld.Simulation.Actors.Utility;

public sealed class EatNpcAction() : NpcAction(
    NpcActionCodes.Eat,
    new UtilityConsideration("Hunger", context => context.Npc.Hunger / 100m),
    new UtilityConsideration("FoodAvailability", context => context.FoodAvailability))
{
    public override NpcActionEligibility CheckEligibility(NpcDecisionContext context) =>
        context.Npc.Hunger <= 0m
            ? NpcActionEligibility.Ineligible("NPC is not hungry.")
            : context.FoodAvailability <= 0m
                ? NpcActionEligibility.Ineligible("No food is available.")
                : NpcActionEligibility.Eligible;
}

public sealed class SleepNpcAction(UtilityAiOptions options) : NpcAction(
    NpcActionCodes.Sleep,
    new UtilityConsideration("Fatigue", context => (100m - context.Npc.Energy) / 100m),
    new UtilityConsideration("Safety", context => context.Safety))
{
    public override NpcActionEligibility CheckEligibility(NpcDecisionContext context) =>
        context.Npc.Energy >= 100m
            ? NpcActionEligibility.Ineligible("NPC is fully rested.")
            : context.Safety < options.MinimumSafetyForSleep
                ? NpcActionEligibility.Ineligible("The location is unsafe for sleeping.")
                : NpcActionEligibility.Eligible;
}

public sealed class WorkNpcAction(UtilityAiOptions options) : NpcAction(
    NpcActionCodes.Work,
    new UtilityConsideration("MoneyNeed", context =>
        1m - Math.Clamp(context.Npc.Money / options.MoneyComfortTarget, 0m, 1m)),
    new UtilityConsideration("Energy", context => context.Npc.Energy / 100m),
    new UtilityConsideration("Safety", context => context.Safety))
{
    public override NpcActionEligibility CheckEligibility(NpcDecisionContext context) =>
        string.IsNullOrWhiteSpace(context.Npc.Job)
            ? NpcActionEligibility.Ineligible("NPC has no job.")
            : context.Npc.Energy < options.MinimumEnergyForWork
                ? NpcActionEligibility.Ineligible("NPC lacks energy to work.")
                : context.Safety < options.MinimumSafetyForWork
                    ? NpcActionEligibility.Ineligible("The location is unsafe for working.")
                    : NpcActionEligibility.Eligible;
}

public sealed class TravelNpcAction(UtilityAiOptions options) : NpcAction(
    NpcActionCodes.Travel,
    new UtilityConsideration("Opportunity", context => context.TravelOpportunity),
    new UtilityConsideration("Energy", context => context.Npc.Energy / 100m),
    new UtilityConsideration("Safety", context => context.Safety))
{
    public override NpcActionEligibility CheckEligibility(NpcDecisionContext context) =>
        context.TravelOpportunity <= 0m
            ? NpcActionEligibility.Ineligible("No travel objective is available.")
            : context.Npc.Energy < options.MinimumEnergyForTravel
                ? NpcActionEligibility.Ineligible("NPC lacks energy to travel.")
                : NpcActionEligibility.Eligible;
}

public sealed class AttackEnemyNpcAction(UtilityAiOptions options) : NpcAction(
    NpcActionCodes.AttackEnemy,
    new UtilityConsideration("EnemyThreat", context => context.EnemyThreat),
    new UtilityConsideration("Energy", context => context.Npc.Energy / 100m),
    new UtilityConsideration("Danger", context => 1m - context.Safety))
{
    public override NpcActionEligibility CheckEligibility(NpcDecisionContext context) =>
        !context.EnemyPresent
            ? NpcActionEligibility.Ineligible("No enemy is present.")
            : context.Npc.Energy < options.MinimumEnergyForAttack
                ? NpcActionEligibility.Ineligible("NPC lacks energy to attack.")
                : NpcActionEligibility.Eligible;
}

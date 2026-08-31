using RpgWorld.Domain.Actors;
using RpgWorld.Domain.Actors.Memories;

namespace RpgWorld.Simulation.Actors.Utility;

public sealed class DefaultNpcDecisionContextProvider(UtilityAiOptions options) : INpcDecisionContextProvider
{
    public NpcDecisionContext Create(NpcActor npc, IReadOnlyList<NpcMemory>? memories = null)
    {
        ArgumentNullException.ThrowIfNull(npc);
        var foodQuantity = npc.Inventory
            .Where(item => options.FoodItemCodes.Contains(item.ItemCode))
            .Sum(item => item.Quantity);
        var foodAvailability = Math.Clamp(
            foodQuantity / options.FoodQuantityForFullAvailability,
            0m,
            1m);
        var enemies = npc.Relationships
            .Where(relationship =>
                relationship.Hatred > 0 ||
                relationship.Fear > 0 ||
                (relationship.Affinity < 0 &&
                 (string.Equals(relationship.Kind, "enemy", StringComparison.OrdinalIgnoreCase) ||
                  string.Equals(relationship.Kind, "hostile", StringComparison.OrdinalIgnoreCase))))
            .ToArray();
        var enemyThreat = enemies.Length == 0
            ? 0m
            : enemies.Max(relationship => Math.Max(
                Math.Max(relationship.Hatred, relationship.Fear),
                Math.Abs(relationship.Affinity))) / 100m;
        var hostileMemoryThreat = (memories ?? [])
            .Where(memory => memory.EventType is
                NpcMemoryEventTypes.WasAttacked or
                NpcMemoryEventTypes.FamilyMemberKilled or
                NpcMemoryEventTypes.Betrayed)
            .Select(memory => memory.Importance / 100m)
            .DefaultIfEmpty(0m)
            .Max();
        enemyThreat = Math.Max(enemyThreat, hostileMemoryThreat);
        var travelOpportunity =
            (npc.Home is { } home && home != npc.Position) ||
            npc.Goals.Any(goal =>
                string.Equals(goal.Code, "travel", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(goal.Code, "explore", StringComparison.OrdinalIgnoreCase))
                ? 1m
                : 0m;

        return new NpcDecisionContext(
            npc,
            foodAvailability,
            safety: 1m - enemyThreat,
            travelOpportunity,
            enemyPresent: enemies.Length > 0 || hostileMemoryThreat > 0m,
            enemyThreat,
            memories);
    }
}

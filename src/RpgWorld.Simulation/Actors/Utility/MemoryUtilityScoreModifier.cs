using RpgWorld.Domain.Actors.Memories;

namespace RpgWorld.Simulation.Actors.Utility;

public sealed class MemoryUtilityScoreModifier : INpcUtilityScoreModifier
{
    public IReadOnlyList<UtilityScoreModifier> GetModifiers(
        NpcAction action,
        NpcDecisionContext context)
    {
        if (!string.Equals(action.Code, NpcActionCodes.AttackEnemy, StringComparison.OrdinalIgnoreCase)) return [];
        var relevant = context.Memories
            .Where(memory => memory.EventType is
                NpcMemoryEventTypes.WasAttacked or
                NpcMemoryEventTypes.FamilyMemberKilled or
                NpcMemoryEventTypes.Betrayed or
                NpcMemoryEventTypes.Helped or
                NpcMemoryEventTypes.CitySaved)
            .OrderByDescending(memory => memory.Importance)
            .ThenBy(memory => memory.Id)
            .ToArray();
        return relevant.Select(memory =>
        {
            var hostile = memory.EventType is
                NpcMemoryEventTypes.WasAttacked or
                NpcMemoryEventTypes.FamilyMemberKilled or
                NpcMemoryEventTypes.Betrayed;
            var multiplier = hostile
                ? 1m + memory.Importance / 100m
                : Math.Max(0.25m, 1m - memory.Importance / 100m * 0.75m);
            return new UtilityScoreModifier(
                $"Memory:{memory.EventType}:{memory.Id}",
                multiplier,
                $"Importance {memory.Importance}/100 toward {memory.TargetId}");
        }).ToArray();
    }
}

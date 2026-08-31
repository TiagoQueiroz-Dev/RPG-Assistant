namespace RpgWorld.Simulation.Actors.Utility;

public sealed class RelationshipUtilityScoreModifier : INpcUtilityScoreModifier
{
    public IReadOnlyList<UtilityScoreModifier> GetModifiers(NpcAction action, NpcDecisionContext context)
    {
        if (!string.Equals(action.Code, NpcActionCodes.AttackEnemy, StringComparison.OrdinalIgnoreCase)) return [];
        var relationship = context.Npc.Relationships
            .OrderByDescending(candidate => candidate.Hatred)
            .ThenByDescending(candidate => candidate.Fear)
            .FirstOrDefault();
        if (relationship is null) return [];
        var positiveBond = Math.Max(
            Math.Max(relationship.Friendship, relationship.Love),
            relationship.Trust);
        var multiplier = Math.Clamp(
            1m + relationship.Hatred / 100m * 0.75m
               - relationship.Fear / 100m * 0.50m
               - positiveBond / 100m * 0.75m,
            0.10m,
            2m);
        if (multiplier == 1m) return [];
        return [new UtilityScoreModifier(
            $"Relationship:{relationship.ActorId}",
            multiplier,
            $"hatred={relationship.Hatred}, fear={relationship.Fear}, bond={positiveBond}")];
    }
}

using RpgWorld.Domain.Actors.Traits;

namespace RpgWorld.Simulation.Actors.Utility;

public sealed class TraitUtilityScoreModifier(ITraitDefinitionCatalog catalog) : INpcUtilityScoreModifier
{
    public IReadOnlyList<UtilityScoreModifier> GetModifiers(
        NpcAction action,
        NpcDecisionContext context)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(context);
        var modifiers = new List<UtilityScoreModifier>();
        foreach (var traitCode in context.Npc.TraitCodes)
        {
            if (!catalog.TryResolve(traitCode, out var trait) || trait is null ||
                !trait.TryGetMultiplier(action.Code, out var multiplier) ||
                multiplier == 1m)
                continue;
            modifiers.Add(new UtilityScoreModifier(
                $"Trait:{trait.Code}",
                multiplier,
                $"{trait.Name} modifies {action.Code}"));
        }
        return modifiers;
    }
}

namespace RpgWorld.Simulation.Actors.Utility;

public sealed class NpcUtilityDecisionService : INpcUtilityDecisionService
{
    private readonly NpcAction[] _actions;
    private readonly UtilityAiOptions _options;

    public NpcUtilityDecisionService(IEnumerable<NpcAction> actions, UtilityAiOptions options)
    {
        ArgumentNullException.ThrowIfNull(actions);
        _actions = actions.OrderBy(action => action.Code, StringComparer.Ordinal).ToArray();
        if (_actions.Length == 0) throw new ArgumentException("At least one NPC action is required.", nameof(actions));
        if (_actions.Select(action => action.Code).Distinct(StringComparer.OrdinalIgnoreCase).Count() != _actions.Length)
            throw new ArgumentException("NPC action codes must be unique.", nameof(actions));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _options.Validate();
    }

    public NpcDecision? Decide(NpcDecisionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var candidates = _actions.Select(action => Score(action, context)).ToArray();
        var selected = candidates
            .Where(candidate => candidate.IsEligible)
            .OrderByDescending(candidate => candidate.Score)
            .ThenBy(candidate => candidate.ActionCode, StringComparer.Ordinal)
            .FirstOrDefault();
        return selected is null
            ? null
            : new NpcDecision(context.Npc.Id, selected.ActionCode, selected.Score, candidates);
    }

    private NpcActionScore Score(NpcAction action, NpcDecisionContext context)
    {
        var eligibility = action.CheckEligibility(context);
        if (!eligibility.IsEligible)
            return new NpcActionScore(action.Code, false, 0m, [], eligibility.Reason);

        var factors = action.Considerations.Select(consideration =>
        {
            var value = consideration.Evaluate(context);
            var weight = _options.GetWeight(action.Code, consideration.Code);
            if (weight < 0m)
                throw new InvalidOperationException($"Weight for {action.Code}/{consideration.Code} cannot be negative.");
            return new UtilityFactorScore(consideration.Code, value, weight, value * weight);
        }).ToArray();
        var totalWeight = factors.Sum(factor => factor.Weight);
        if (totalWeight <= 0m)
            throw new InvalidOperationException($"Eligible action '{action.Code}' must have a positive total weight.");
        var score = factors.Sum(factor => factor.WeightedValue) / totalWeight;
        return new NpcActionScore(action.Code, true, score, factors, null);
    }
}

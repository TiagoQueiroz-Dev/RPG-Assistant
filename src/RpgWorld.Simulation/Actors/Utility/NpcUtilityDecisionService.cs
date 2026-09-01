using RpgWorld.Simulation.Engine;

namespace RpgWorld.Simulation.Actors.Utility;

public sealed class NpcUtilityDecisionService : INpcUtilityDecisionService
{
    private readonly NpcAction[] _actions;
    private readonly INpcUtilityScoreModifier[] _modifiers;
    private readonly UtilityAiOptions _options;
    private readonly ISimulationRandom? _random;

    public NpcUtilityDecisionService(IEnumerable<NpcAction> actions, UtilityAiOptions options)
        : this(actions, options, []) { }

    public NpcUtilityDecisionService(
        IEnumerable<NpcAction> actions,
        UtilityAiOptions options,
        IEnumerable<INpcUtilityScoreModifier> modifiers)
        : this(actions, options, modifiers, null) { }

    public NpcUtilityDecisionService(
        IEnumerable<NpcAction> actions,
        UtilityAiOptions options,
        IEnumerable<INpcUtilityScoreModifier> modifiers,
        ISimulationRandom? random)
    {
        ArgumentNullException.ThrowIfNull(actions);
        _actions = actions.OrderBy(action => action.Code, StringComparer.Ordinal).ToArray();
        if (_actions.Length == 0) throw new ArgumentException("At least one NPC action is required.", nameof(actions));
        if (_actions.Select(action => action.Code).Distinct(StringComparer.OrdinalIgnoreCase).Count() != _actions.Length)
            throw new ArgumentException("NPC action codes must be unique.", nameof(actions));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _options.Validate();
        ArgumentNullException.ThrowIfNull(modifiers);
        _modifiers = modifiers.ToArray();
        _random = random;
    }

    public NpcDecision? Decide(NpcDecisionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var candidates = _actions.Select(action => Score(action, context)).ToArray();
        var eligible = candidates
            .Where(candidate => candidate.IsEligible)
            .OrderByDescending(candidate => candidate.Score)
            .ThenBy(candidate => candidate.ActionCode, StringComparer.Ordinal)
            .ToArray();
        if (eligible.Length == 0) return null;
        var tied = eligible.TakeWhile(candidate => candidate.Score == eligible[0].Score).ToArray();
        var selected = tied.Length == 1 || _random is null
            ? tied[0]
            : tied[_random.Next(tied.Length)];
        return new NpcDecision(context.Npc.Id, selected.ActionCode, selected.Score, candidates);
    }

    private NpcActionScore Score(NpcAction action, NpcDecisionContext context)
    {
        var eligibility = action.CheckEligibility(context);
        if (!eligibility.IsEligible)
            return new NpcActionScore(action.Code, false, 0m, 0m, [], [], eligibility.Reason);

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
        var baseScore = factors.Sum(factor => factor.WeightedValue) / totalWeight;
        var modifiers = _modifiers
            .SelectMany(modifier => modifier.GetModifiers(action, context))
            .ToArray();
        if (modifiers.Any(modifier => modifier.Multiplier <= 0m))
            throw new InvalidOperationException($"Score modifiers for '{action.Code}' must be positive.");
        var score = Math.Clamp(
            modifiers.Aggregate(baseScore, (current, modifier) => current * modifier.Multiplier),
            0m,
            1m);
        return new NpcActionScore(action.Code, true, baseScore, score, factors, modifiers, null);
    }
}

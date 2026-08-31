using RpgWorld.Domain.Actors;

namespace RpgWorld.Simulation.Actors.Utility;

public sealed record NpcDecisionContext
{
    public NpcDecisionContext(
        NpcActor npc,
        decimal foodAvailability,
        decimal safety,
        decimal travelOpportunity,
        bool enemyPresent,
        decimal enemyThreat)
    {
        Npc = npc ?? throw new ArgumentNullException(nameof(npc));
        FoodAvailability = Normalize(foodAvailability, nameof(foodAvailability));
        Safety = Normalize(safety, nameof(safety));
        TravelOpportunity = Normalize(travelOpportunity, nameof(travelOpportunity));
        EnemyPresent = enemyPresent;
        EnemyThreat = Normalize(enemyThreat, nameof(enemyThreat));
    }

    public NpcActor Npc { get; }
    public decimal FoodAvailability { get; }
    public decimal Safety { get; }
    public decimal TravelOpportunity { get; }
    public bool EnemyPresent { get; }
    public decimal EnemyThreat { get; }

    private static decimal Normalize(decimal value, string parameterName) =>
        value is < 0m or > 1m
            ? throw new ArgumentOutOfRangeException(parameterName, "Decision factors must be between zero and one.")
            : value;
}

public sealed class UtilityConsideration
{
    private readonly Func<NpcDecisionContext, decimal> _evaluate;

    public UtilityConsideration(string code, Func<NpcDecisionContext, decimal> evaluate)
    {
        if (string.IsNullOrWhiteSpace(code)) throw new ArgumentException("Consideration code is required.", nameof(code));
        Code = code.Trim();
        _evaluate = evaluate ?? throw new ArgumentNullException(nameof(evaluate));
    }

    public string Code { get; }

    public decimal Evaluate(NpcDecisionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var value = _evaluate(context);
        return value is < 0m or > 1m
            ? throw new InvalidOperationException($"Consideration '{Code}' produced {value}; expected a value between zero and one.")
            : value;
    }
}

public abstract class NpcAction
{
    protected NpcAction(string code, params UtilityConsideration[] considerations)
    {
        if (string.IsNullOrWhiteSpace(code)) throw new ArgumentException("Action code is required.", nameof(code));
        if (considerations is null || considerations.Length == 0)
            throw new ArgumentException("An action requires at least one consideration.", nameof(considerations));
        if (considerations.Select(item => item.Code).Distinct(StringComparer.OrdinalIgnoreCase).Count() != considerations.Length)
            throw new ArgumentException("Consideration codes must be unique within an action.", nameof(considerations));
        Code = code.Trim();
        Considerations = considerations;
    }

    public string Code { get; }
    public IReadOnlyList<UtilityConsideration> Considerations { get; }

    public abstract NpcActionEligibility CheckEligibility(NpcDecisionContext context);
}

public sealed record NpcActionEligibility(bool IsEligible, string? Reason = null)
{
    public static NpcActionEligibility Eligible { get; } = new(true);

    public static NpcActionEligibility Ineligible(string reason) =>
        string.IsNullOrWhiteSpace(reason)
            ? throw new ArgumentException("Ineligibility reason is required.", nameof(reason))
            : new(false, reason.Trim());
}

public sealed record UtilityFactorScore(
    string Code,
    decimal Value,
    decimal Weight,
    decimal WeightedValue);

public sealed record NpcActionScore(
    string ActionCode,
    bool IsEligible,
    decimal Score,
    IReadOnlyList<UtilityFactorScore> Factors,
    string? IneligibilityReason);

public sealed record NpcDecision(
    Guid ActorId,
    string ActionCode,
    decimal Score,
    IReadOnlyList<NpcActionScore> Candidates)
{
    public string Explain()
    {
        var selected = Candidates.Single(candidate =>
            string.Equals(candidate.ActionCode, ActionCode, StringComparison.OrdinalIgnoreCase));
        var factors = string.Join(", ", selected.Factors.Select(factor =>
            FormattableString.Invariant(
                $"{factor.Code}={factor.Value:0.0000} (weight={factor.Weight:0.####}, contribution={factor.WeightedValue:0.0000})")));
        return FormattableString.Invariant($"{ActionCode} selected with score {Score:0.0000}: {factors}.");
    }
}

public interface INpcUtilityDecisionService
{
    NpcDecision? Decide(NpcDecisionContext context);
}

public interface INpcDecisionContextProvider
{
    NpcDecisionContext Create(NpcActor npc);
}

namespace RpgWorld.Simulation.Actors.Utility;

public sealed class UtilityAiOptions
{
    public decimal MoneyComfortTarget { get; init; } = 100m;
    public decimal FoodQuantityForFullAvailability { get; init; } = 3m;
    public decimal MinimumSafetyForSleep { get; init; } = 0.25m;
    public decimal MinimumSafetyForWork { get; init; } = 0.25m;
    public decimal MinimumEnergyForWork { get; init; } = 20m;
    public decimal MinimumEnergyForTravel { get; init; } = 20m;
    public decimal MinimumEnergyForAttack { get; init; } = 25m;

    public ISet<string> FoodItemCodes { get; init; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "food",
        "meal",
        "ration"
    };

    public IDictionary<string, IDictionary<string, decimal>> ActionWeights { get; init; } =
        CreateDefaultWeights();

    public decimal GetWeight(string actionCode, string considerationCode)
    {
        var action = ActionWeights.FirstOrDefault(entry =>
            string.Equals(entry.Key, actionCode, StringComparison.OrdinalIgnoreCase)).Value;
        if (action is null) return 1m;
        var factor = action.FirstOrDefault(entry =>
            string.Equals(entry.Key, considerationCode, StringComparison.OrdinalIgnoreCase));
        return factor.Key is null ? 1m : factor.Value;
    }

    public void Validate()
    {
        if (MoneyComfortTarget <= 0m) throw new ArgumentOutOfRangeException(nameof(MoneyComfortTarget));
        if (FoodQuantityForFullAvailability <= 0m) throw new ArgumentOutOfRangeException(nameof(FoodQuantityForFullAvailability));
        ValidateNormalized(MinimumSafetyForSleep, nameof(MinimumSafetyForSleep));
        ValidateNormalized(MinimumSafetyForWork, nameof(MinimumSafetyForWork));
        ValidateNeed(MinimumEnergyForWork, nameof(MinimumEnergyForWork));
        ValidateNeed(MinimumEnergyForTravel, nameof(MinimumEnergyForTravel));
        ValidateNeed(MinimumEnergyForAttack, nameof(MinimumEnergyForAttack));
        if (FoodItemCodes.Count == 0 || FoodItemCodes.Any(string.IsNullOrWhiteSpace))
            throw new ArgumentException("At least one valid food item code is required.", nameof(FoodItemCodes));
        if (ActionWeights.Any(action =>
            string.IsNullOrWhiteSpace(action.Key) ||
            action.Value.Count == 0 ||
            action.Value.Any(factor => string.IsNullOrWhiteSpace(factor.Key) || factor.Value < 0m)))
            throw new ArgumentException("Action and consideration weights must have names and non-negative values.", nameof(ActionWeights));
    }

    private static Dictionary<string, IDictionary<string, decimal>> CreateDefaultWeights() =>
        new(StringComparer.OrdinalIgnoreCase)
        {
            [NpcActionCodes.Eat] = Weights(("Hunger", 0.70m), ("FoodAvailability", 0.30m)),
            [NpcActionCodes.Sleep] = Weights(("Fatigue", 0.75m), ("Safety", 0.25m)),
            [NpcActionCodes.Work] = Weights(("MoneyNeed", 0.55m), ("Energy", 0.25m), ("Safety", 0.20m)),
            [NpcActionCodes.Travel] = Weights(("Opportunity", 0.50m), ("Energy", 0.30m), ("Safety", 0.20m)),
            [NpcActionCodes.AttackEnemy] = Weights(("EnemyThreat", 0.50m), ("Energy", 0.20m), ("Danger", 0.30m))
        };

    private static Dictionary<string, decimal> Weights(params (string Code, decimal Weight)[] values) =>
        values.ToDictionary(value => value.Code, value => value.Weight, StringComparer.OrdinalIgnoreCase);

    private static void ValidateNormalized(decimal value, string parameterName)
    {
        if (value is < 0m or > 1m) throw new ArgumentOutOfRangeException(parameterName);
    }

    private static void ValidateNeed(decimal value, string parameterName)
    {
        if (value is < 0m or > 100m) throw new ArgumentOutOfRangeException(parameterName);
    }
}

public static class NpcActionCodes
{
    public const string Eat = "Eat";
    public const string Sleep = "Sleep";
    public const string Work = "Work";
    public const string Travel = "Travel";
    public const string AttackEnemy = "AttackEnemy";
}

namespace RpgWorld.Domain.Worlds.Cities;

public sealed record CityHistoryEntry
{
    public CityHistoryEntry(
        Guid id,
        string eventType,
        string description,
        int population,
        DateTimeOffset occurredAtUtc,
        Dictionary<string, string>? metadata = null)
    {
        if (id == Guid.Empty) throw new ArgumentException("History identifier cannot be empty.", nameof(id));
        if (string.IsNullOrWhiteSpace(eventType)) throw new ArgumentException("History event type is required.", nameof(eventType));
        if (string.IsNullOrWhiteSpace(description)) throw new ArgumentException("History description is required.", nameof(description));
        if (population < 0) throw new ArgumentOutOfRangeException(nameof(population));
        Id = id;
        EventType = eventType.Trim().ToLowerInvariant();
        Description = description.Trim();
        Population = population;
        OccurredAtUtc = occurredAtUtc.ToUniversalTime();
        Metadata = metadata ?? [];
    }

    public Guid Id { get; init; }
    public string EventType { get; init; }
    public string Description { get; init; }
    public int Population { get; init; }
    public DateTimeOffset OccurredAtUtc { get; init; }
    public Dictionary<string, string> Metadata { get; init; }
}

public static class CityHistoryEventTypes
{
    public const string Founded = "founded";
    public const string Growth = "growth";
    public const string Decline = "decline";
    public const string Crisis = "crisis";
    public const string CrisisResolved = "crisis-resolved";
    public const string ResidentAssociated = "resident-associated";
    public const string ResourceShortage = "resource-shortage";
    public const string ResourceSurplus = "resource-surplus";
    public const string EconomyBalanced = "economy-balanced";
    public const string TradeRoutesChanged = "trade-routes-changed";
    public const string SatisfactionChanged = "satisfaction-changed";
    public const string Destroyed = "destroyed";
}

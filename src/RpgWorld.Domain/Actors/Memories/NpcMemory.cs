namespace RpgWorld.Domain.Actors.Memories;

public sealed class NpcMemory
{
    private Dictionary<string, string> _payload = new(StringComparer.OrdinalIgnoreCase);

    private NpcMemory() { }

    private NpcMemory(
        Guid actorId,
        Guid worldId,
        string eventType,
        Guid? targetId,
        int importance,
        DateTimeOffset createdAt,
        DateTimeOffset? expiresAt,
        IReadOnlyDictionary<string, string>? payload)
    {
        if (actorId == Guid.Empty) throw new ArgumentException("NPC identifier is required.", nameof(actorId));
        if (worldId == Guid.Empty) throw new ArgumentException("World identifier is required.", nameof(worldId));
        if (string.IsNullOrWhiteSpace(eventType)) throw new ArgumentException("Memory event type is required.", nameof(eventType));
        if (eventType.Length > 80) throw new ArgumentException("Memory event type is too long.", nameof(eventType));
        if (targetId == Guid.Empty) throw new ArgumentException("Memory target cannot be empty.", nameof(targetId));
        if (importance is < 1 or > 100) throw new ArgumentOutOfRangeException(nameof(importance));
        var created = createdAt.ToUniversalTime();
        var expiration = expiresAt?.ToUniversalTime();
        if (expiration <= created) throw new ArgumentOutOfRangeException(nameof(expiresAt), "Expiration must follow creation.");
        Id = Guid.CreateVersion7();
        ActorId = actorId;
        WorldId = worldId;
        EventType = eventType.Trim().ToLowerInvariant();
        TargetId = targetId;
        Importance = importance;
        CreatedAt = created;
        ExpiresAt = expiration;
        _payload = NormalizePayload(payload);
    }

    public Guid Id { get; private set; }
    public Guid ActorId { get; private set; }
    public Guid WorldId { get; private set; }
    public string EventType { get; private set; } = string.Empty;
    public Guid? TargetId { get; private set; }
    public int Importance { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? ExpiresAt { get; private set; }
    public IReadOnlyDictionary<string, string> Payload => _payload;

    public static NpcMemory Create(
        Guid actorId,
        Guid worldId,
        string eventType,
        Guid? targetId,
        int importance,
        DateTimeOffset createdAt,
        DateTimeOffset? expiresAt = null,
        IReadOnlyDictionary<string, string>? payload = null) =>
        new(actorId, worldId, eventType, targetId, importance, createdAt, expiresAt, payload);

    public bool IsExpired(DateTimeOffset instant) =>
        ExpiresAt is { } expiration && expiration <= instant.ToUniversalTime();

    private static Dictionary<string, string> NormalizePayload(IReadOnlyDictionary<string, string>? payload)
    {
        if (payload is null) return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (payload.Count > 16) throw new ArgumentException("Memory payload supports at most 16 entries.", nameof(payload));
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in payload)
        {
            if (string.IsNullOrWhiteSpace(key) || key.Length > 80)
                throw new ArgumentException("Memory payload keys must contain 1 to 80 characters.", nameof(payload));
            if (value is null || value.Length > 500)
                throw new ArgumentException("Memory payload values cannot exceed 500 characters.", nameof(payload));
            if (!result.TryAdd(key.Trim(), value))
                throw new ArgumentException($"Duplicate memory payload key '{key}'.", nameof(payload));
        }
        return result;
    }
}

public static class NpcMemoryEventTypes
{
    public const string WasAttacked = "was-attacked";
    public const string FamilyMemberKilled = "family-member-killed";
    public const string Betrayed = "betrayed";
    public const string Helped = "helped";
    public const string CitySaved = "city-saved";
}

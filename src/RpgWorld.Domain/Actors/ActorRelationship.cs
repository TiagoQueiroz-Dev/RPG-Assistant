using System.Text.Json.Serialization;

namespace RpgWorld.Domain.Actors;

public sealed record ActorRelationship
{
    public const int MinimumValue = -100;
    public const int MaximumValue = 100;
    public const int MaximumHistoryEntries = 50;

    [JsonConstructor]
    public ActorRelationship(
        Guid actorId,
        string kind,
        int affinity,
        int friendship = 0,
        int fear = 0,
        int respect = 0,
        int love = 0,
        int hatred = 0,
        int trust = 0,
        IReadOnlyList<ActorRelationshipChange>? history = null)
    {
        if (actorId == Guid.Empty) throw new ArgumentException("Related actor is required.", nameof(actorId));
        if (string.IsNullOrWhiteSpace(kind)) throw new ArgumentException("Relationship kind is required.", nameof(kind));
        ActorId = actorId;
        Kind = kind.Trim();
        Affinity = ValidateValue(affinity, nameof(affinity));
        Friendship = ValidateValue(friendship, nameof(friendship));
        Fear = ValidateValue(fear, nameof(fear));
        Respect = ValidateValue(respect, nameof(respect));
        Love = ValidateValue(love, nameof(love));
        Hatred = ValidateValue(hatred, nameof(hatred));
        Trust = ValidateValue(trust, nameof(trust));
        History = (history ?? []).TakeLast(MaximumHistoryEntries).ToArray();
    }

    public Guid ActorId { get; init; }
    public string Kind { get; init; }
    public int Affinity { get; init; }
    public int Friendship { get; init; }
    public int Fear { get; init; }
    public int Respect { get; init; }
    public int Love { get; init; }
    public int Hatred { get; init; }
    public int Trust { get; init; }
    public IReadOnlyList<ActorRelationshipChange> History { get; init; }

    public static ActorRelationship Neutral(Guid actorId) => new(actorId, "neutral", 0);

    public ActorRelationship Apply(ActorRelationshipModifier modifier, DateTimeOffset occurredAt)
    {
        ArgumentNullException.ThrowIfNull(modifier);
        var friendship = Clamp(Friendship + modifier.Friendship);
        var fear = Clamp(Fear + modifier.Fear);
        var respect = Clamp(Respect + modifier.Respect);
        var love = Clamp(Love + modifier.Love);
        var hatred = Clamp(Hatred + modifier.Hatred);
        var trust = Clamp(Trust + modifier.Trust);
        var affinity = Clamp((friendship + respect + love + trust - fear - hatred) / 6);
        var history = History.Append(new ActorRelationshipChange(
                occurredAt.ToUniversalTime(),
                modifier.Reason,
                modifier.Friendship,
                modifier.Fear,
                modifier.Respect,
                modifier.Love,
                modifier.Hatred,
                modifier.Trust))
            .TakeLast(MaximumHistoryEntries)
            .ToArray();
        return new ActorRelationship(
            ActorId,
            DetermineKind(friendship, fear, love, hatred, trust),
            affinity,
            friendship,
            fear,
            respect,
            love,
            hatred,
            trust,
            history);
    }

    private static string DetermineKind(int friendship, int fear, int love, int hatred, int trust)
    {
        if (hatred >= 50) return "enemy";
        if (love >= 50) return "loved";
        if (friendship >= 50) return "friend";
        if (fear >= 50) return "feared";
        if (trust >= 50) return "trusted";
        return "neutral";
    }

    private static int ValidateValue(int value, string parameterName) =>
        value is < MinimumValue or > MaximumValue
            ? throw new ArgumentOutOfRangeException(parameterName)
            : value;

    private static int Clamp(int value) => Math.Clamp(value, MinimumValue, MaximumValue);
}

public sealed record ActorRelationshipModifier
{
    public ActorRelationshipModifier(
        string reason,
        int friendship = 0,
        int fear = 0,
        int respect = 0,
        int love = 0,
        int hatred = 0,
        int trust = 0)
    {
        if (string.IsNullOrWhiteSpace(reason)) throw new ArgumentException("Relationship change reason is required.", nameof(reason));
        if (reason.Length > 160) throw new ArgumentException("Relationship change reason is too long.", nameof(reason));
        Reason = reason.Trim();
        Friendship = ValidateDelta(friendship, nameof(friendship));
        Fear = ValidateDelta(fear, nameof(fear));
        Respect = ValidateDelta(respect, nameof(respect));
        Love = ValidateDelta(love, nameof(love));
        Hatred = ValidateDelta(hatred, nameof(hatred));
        Trust = ValidateDelta(trust, nameof(trust));
        if (Friendship == 0 && Fear == 0 && Respect == 0 && Love == 0 && Hatred == 0 && Trust == 0)
            throw new ArgumentException("At least one relationship dimension must change.");
    }

    public string Reason { get; }
    public int Friendship { get; }
    public int Fear { get; }
    public int Respect { get; }
    public int Love { get; }
    public int Hatred { get; }
    public int Trust { get; }

    private static int ValidateDelta(int value, string parameterName) =>
        value is < -200 or > 200 ? throw new ArgumentOutOfRangeException(parameterName) : value;
}

public sealed record ActorRelationshipChange(
    DateTimeOffset OccurredAt,
    string Reason,
    int Friendship,
    int Fear,
    int Respect,
    int Love,
    int Hatred,
    int Trust);

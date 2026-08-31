namespace RpgWorld.Domain.Worlds.Factions;

public enum FactionRelationKind
{
    Neutral = 0,
    Alliance = 1,
    Allied = Alliance,
    Hostile = 2,
    War = 3,
    Vassal = 4
}

public enum FactionRelationModifierSource { Event, Border, Trade, Leadership, History }

public sealed record FactionRelationModifier
{
    public FactionRelationModifier(
        FactionRelationModifierSource source,
        string reason,
        int affinityDelta = 0,
        int tensionDelta = 0,
        Guid? sourceEventId = null,
        bool? vassalage = null)
    {
        if (!Enum.IsDefined(source)) throw new ArgumentOutOfRangeException(nameof(source));
        if (string.IsNullOrWhiteSpace(reason)) throw new ArgumentException("Modifier reason is required.", nameof(reason));
        if (reason.Trim().Length > 500) throw new ArgumentException("Modifier reason cannot exceed 500 characters.", nameof(reason));
        if (affinityDelta is < -200 or > 200) throw new ArgumentOutOfRangeException(nameof(affinityDelta));
        if (tensionDelta is < -200 or > 200) throw new ArgumentOutOfRangeException(nameof(tensionDelta));
        if (sourceEventId == Guid.Empty) throw new ArgumentException("Source event identifier cannot be empty.", nameof(sourceEventId));
        if (affinityDelta == 0 && tensionDelta == 0 && vassalage is null)
            throw new ArgumentException("Modifier must change affinity, tension or vassalage.");
        Source = source;
        Reason = reason.Trim();
        AffinityDelta = affinityDelta;
        TensionDelta = tensionDelta;
        SourceEventId = sourceEventId;
        Vassalage = vassalage;
    }

    public FactionRelationModifierSource Source { get; }
    public string Reason { get; }
    public int AffinityDelta { get; }
    public int TensionDelta { get; }
    public Guid? SourceEventId { get; }
    public bool? Vassalage { get; }
}

public sealed record FactionRelationChange(
    Guid Id,
    FactionRelationModifierSource Source,
    string Reason,
    int AffinityDelta,
    int TensionDelta,
    int PreviousAffinity,
    int Affinity,
    int PreviousTension,
    int Tension,
    FactionRelationKind PreviousState,
    FactionRelationKind State,
    Guid? SourceEventId,
    DateTimeOffset OccurredAtUtc);

public sealed record FactionRelation
{
    public const int MaximumHistoryEntries = 200;

    public FactionRelation(
        Guid targetFactionId,
        FactionRelationKind kind,
        int score,
        DateTimeOffset updatedAtUtc,
        int tension = 0,
        bool isVassal = false,
        IReadOnlyList<FactionRelationChange>? history = null)
    {
        if (targetFactionId == Guid.Empty) throw new ArgumentException("Target faction is required.", nameof(targetFactionId));
        if (!Enum.IsDefined(kind)) throw new ArgumentOutOfRangeException(nameof(kind));
        if (score is < -100 or > 100) throw new ArgumentOutOfRangeException(nameof(score));
        if (tension is < 0 or > 100) throw new ArgumentOutOfRangeException(nameof(tension));
        TargetFactionId = targetFactionId;
        Kind = kind;
        Score = score;
        Tension = tension;
        IsVassal = isVassal;
        UpdatedAtUtc = updatedAtUtc.ToUniversalTime();
        History = (history ?? []).TakeLast(MaximumHistoryEntries).ToArray();
    }

    public Guid TargetFactionId { get; init; }
    public FactionRelationKind Kind { get; init; }
    public int Score { get; init; }
    public int Affinity => Score;
    public int Tension { get; init; }
    public bool IsVassal { get; init; }
    public DateTimeOffset UpdatedAtUtc { get; init; }
    public IReadOnlyList<FactionRelationChange> History { get; init; }

    public static FactionRelation Neutral(Guid targetFactionId, DateTimeOffset occurredAtUtc) =>
        new(targetFactionId, FactionRelationKind.Neutral, 0, occurredAtUtc);

    public FactionRelation Apply(FactionRelationModifier modifier, DateTimeOffset occurredAtUtc)
    {
        ArgumentNullException.ThrowIfNull(modifier);
        var instant = occurredAtUtc.ToUniversalTime();
        if (instant < UpdatedAtUtc)
            throw new ArgumentOutOfRangeException(nameof(occurredAtUtc), "Diplomatic relation time cannot move backwards.");
        var affinity = Math.Clamp(checked(Score + modifier.AffinityDelta), -100, 100);
        var tension = Math.Clamp(checked(Tension + modifier.TensionDelta), 0, 100);
        var isVassal = modifier.Vassalage ?? IsVassal;
        var state = isVassal ? FactionRelationKind.Vassal : ResolveState(affinity, tension);
        var change = new FactionRelationChange(
            Guid.CreateVersion7(), modifier.Source, modifier.Reason,
            modifier.AffinityDelta, modifier.TensionDelta, Score, affinity, Tension, tension,
            Kind, state, modifier.SourceEventId, instant);
        return new FactionRelation(
            TargetFactionId, state, affinity, instant, tension, isVassal,
            History.Append(change).TakeLast(MaximumHistoryEntries).ToArray());
    }

    public static FactionRelationKind ResolveState(int affinity, int tension)
    {
        if (affinity is < -100 or > 100) throw new ArgumentOutOfRangeException(nameof(affinity));
        if (tension is < 0 or > 100) throw new ArgumentOutOfRangeException(nameof(tension));
        if (tension >= 80 || affinity <= -80) return FactionRelationKind.War;
        if (tension >= 50 || affinity <= -30) return FactionRelationKind.Hostile;
        if (affinity >= 60 && tension <= 30) return FactionRelationKind.Alliance;
        return FactionRelationKind.Neutral;
    }
}

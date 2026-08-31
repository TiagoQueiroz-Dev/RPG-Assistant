using RpgWorld.Domain.Actors.Memories;

namespace RpgWorld.Application.Actors.Memories;

public sealed class NpcMemoryOptions
{
    public int PermanentImportance { get; init; } = 80;
    public TimeSpan BaseRetention { get; init; } = TimeSpan.FromDays(30);
    public int MinimumDecisionImportance { get; init; } = 20;
    public int MinimumInspectorImportance { get; init; } = 20;
    public ISet<string> EnabledEventTypes { get; init; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        NpcMemoryEventTypes.WasAttacked,
        NpcMemoryEventTypes.FamilyMemberKilled
    };

    public DateTimeOffset? CalculateExpiration(DateTimeOffset createdAt, int importance)
    {
        Validate();
        if (importance is < 1 or > 100) throw new ArgumentOutOfRangeException(nameof(importance));
        return importance >= PermanentImportance
            ? null
            : createdAt.ToUniversalTime().Add(TimeSpan.FromTicks(
                checked(BaseRetention.Ticks * importance / 100)));
    }

    public void Validate()
    {
        if (PermanentImportance is < 1 or > 100) throw new ArgumentOutOfRangeException(nameof(PermanentImportance));
        if (BaseRetention <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(BaseRetention));
        if (MinimumDecisionImportance is < 1 or > 100) throw new ArgumentOutOfRangeException(nameof(MinimumDecisionImportance));
        if (MinimumInspectorImportance is < 1 or > 100) throw new ArgumentOutOfRangeException(nameof(MinimumInspectorImportance));
        if (EnabledEventTypes.Any(string.IsNullOrWhiteSpace))
            throw new ArgumentException("Enabled memory event types must be valid.", nameof(EnabledEventTypes));
    }
}

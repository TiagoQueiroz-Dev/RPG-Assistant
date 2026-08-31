namespace RpgWorld.Domain.Actors;

public sealed record NpcGoal
{
    public NpcGoal(string code, int priority, Guid? targetId = null)
    {
        if (string.IsNullOrWhiteSpace(code)) throw new ArgumentException("Goal code is required.", nameof(code));
        if (priority is < 1 or > 100) throw new ArgumentOutOfRangeException(nameof(priority));
        if (targetId == Guid.Empty) throw new ArgumentException("Goal target cannot be empty.", nameof(targetId));
        Code = code.Trim();
        Priority = priority;
        TargetId = targetId;
    }

    public string Code { get; init; }
    public int Priority { get; init; }
    public Guid? TargetId { get; init; }
}

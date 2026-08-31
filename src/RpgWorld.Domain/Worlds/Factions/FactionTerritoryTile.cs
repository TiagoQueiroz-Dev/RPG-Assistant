namespace RpgWorld.Domain.Worlds.Factions;

public sealed class FactionTerritoryTile
{
    private FactionTerritoryTile() { }

    private FactionTerritoryTile(Guid factionId, Position position)
    {
        Id = Guid.CreateVersion7();
        FactionId = factionId;
        WorldId = position.WorldId;
        X = position.X;
        Y = position.Y;
    }

    public Guid Id { get; private set; }
    public Guid FactionId { get; private set; }
    public Guid WorldId { get; private set; }
    public int X { get; private set; }
    public int Y { get; private set; }
    public bool IsActive { get; private set; } = true;
    public DateTimeOffset? ReleasedAtUtc { get; private set; }
    public Position Position => new(WorldId, X, Y);

    internal static FactionTerritoryTile Create(Guid factionId, Position position) => new(factionId, position);

    internal void Release(DateTimeOffset releasedAtUtc)
    {
        if (!IsActive) return;
        IsActive = false;
        ReleasedAtUtc = releasedAtUtc.ToUniversalTime();
    }
}

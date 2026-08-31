namespace RpgWorld.Domain.Worlds.Cities;

public sealed class CityTerritoryTile
{
    private CityTerritoryTile() { }

    private CityTerritoryTile(Guid cityId, Position position)
    {
        Id = Guid.CreateVersion7();
        CityId = cityId;
        WorldId = position.WorldId;
        X = position.X;
        Y = position.Y;
    }

    public Guid Id { get; private set; }
    public Guid CityId { get; private set; }
    public Guid WorldId { get; private set; }
    public int X { get; private set; }
    public int Y { get; private set; }
    public bool IsActive { get; private set; } = true;
    public DateTimeOffset? ReleasedAtUtc { get; private set; }
    public Position Position => new(WorldId, X, Y);

    internal static CityTerritoryTile Create(Guid cityId, Position position) => new(cityId, position);

    internal void Release(DateTimeOffset releasedAtUtc)
    {
        if (!IsActive) return;
        IsActive = false;
        ReleasedAtUtc = releasedAtUtc.ToUniversalTime();
    }
}

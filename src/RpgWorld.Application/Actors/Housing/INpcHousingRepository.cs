using RpgWorld.Domain.Actors;
using RpgWorld.Domain.Actors.Housing;
using RpgWorld.Domain.Worlds;

namespace RpgWorld.Application.Actors.Housing;

public interface INpcHousingRepository
{
    Task<World?> GetWorldAsync(Guid worldId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<NpcActor>> ListHomelessAsync(Guid worldId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<HousingConstruction>> ListInProgressAsync(Guid worldId, CancellationToken cancellationToken = default);
    Task<NpcActor?> GetNpcAsync(Guid actorId, CancellationToken cancellationToken = default);
    Task<Tile?> FindBuildableTileAsync(Guid worldId, int originX, int originY, int radius, IReadOnlyCollection<string> allowedTerrains, CancellationToken cancellationToken = default);
    Task<Tile?> GetTileAsync(Position position, CancellationToken cancellationToken = default);
    void Add(HousingConstruction construction);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

public sealed class NpcHousingOptions
{
    public int RequiredWood { get; init; } = 4;
    public int RequiredStone { get; init; } = 2;
    public int SearchRadius { get; init; } = 8;
    public IReadOnlyCollection<string> AllowedTerrainCodes { get; init; } =
        ["plains", "woodland", "rocky", "sand", "wetland", "snow", "volcanic-rock"];

    public void Validate()
    {
        if (RequiredWood <= 0 || RequiredStone <= 0) throw new ArgumentOutOfRangeException(nameof(RequiredWood));
        if (SearchRadius < 1) throw new ArgumentOutOfRangeException(nameof(SearchRadius));
        if (AllowedTerrainCodes.Count == 0 || AllowedTerrainCodes.Any(string.IsNullOrWhiteSpace))
            throw new ArgumentException("At least one buildable terrain is required.", nameof(AllowedTerrainCodes));
    }
}

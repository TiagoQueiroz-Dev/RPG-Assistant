using RpgWorld.Domain.Actors;
using RpgWorld.Domain.Worlds;
using RpgWorld.Domain.Worlds.Cities;

namespace RpgWorld.Application.Worlds.Cities;

public interface ICityRepository
{
    Task<World?> GetWorldAsync(Guid worldId, CancellationToken cancellationToken = default);
    Task<City?> GetAsync(Guid cityId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<City>> ListByWorldAsync(Guid worldId, CancellationToken cancellationToken = default);
    Task<NpcActor?> GetNpcAsync(Guid actorId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<NpcActor>> ListResidentsAsync(Guid cityId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Tile>> ListTilesAsync(Guid worldId, IReadOnlyCollection<Position> positions, CancellationToken cancellationToken = default);
    Task<bool> TerritoryOverlapsAsync(Guid worldId, IReadOnlyCollection<Position> positions, CancellationToken cancellationToken = default);
    void Add(City city);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

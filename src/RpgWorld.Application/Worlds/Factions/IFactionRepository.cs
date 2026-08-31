using RpgWorld.Domain.Actors;
using RpgWorld.Domain.Worlds;
using RpgWorld.Domain.Worlds.Cities;
using RpgWorld.Domain.Worlds.Factions;

namespace RpgWorld.Application.Worlds.Factions;

public interface IFactionRepository
{
    Task<World?> GetWorldAsync(Guid worldId, CancellationToken cancellationToken = default);
    Task<Faction?> GetAsync(Guid factionId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Faction>> ListByWorldAsync(Guid worldId, CancellationToken cancellationToken = default);
    Task<bool> NameExistsAsync(Guid worldId, string name, CancellationToken cancellationToken = default);
    Task<Actor?> GetActorAsync(Guid actorId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Actor>> ListMembersAsync(Guid factionId, CancellationToken cancellationToken = default);
    Task<City?> GetCityAsync(Guid cityId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<City>> ListCitiesAsync(Guid factionId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Tile>> ListTilesAsync(
        Guid worldId,
        IReadOnlyCollection<Position> positions,
        CancellationToken cancellationToken = default);
    Task<bool> TerritoryOverlapsAsync(
        Guid worldId,
        IReadOnlyCollection<Position> positions,
        Guid? excludingFactionId = null,
        CancellationToken cancellationToken = default);
    void Add(Faction faction);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

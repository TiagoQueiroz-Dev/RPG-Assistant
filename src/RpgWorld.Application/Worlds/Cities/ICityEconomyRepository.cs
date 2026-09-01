using RpgWorld.Domain.Actors;
using RpgWorld.Domain.Worlds.Cities;
using RpgWorld.Domain.Worlds.Resources;

namespace RpgWorld.Application.Worlds.Cities;

public interface ICityEconomyRepository
{
    Task<IReadOnlyList<City>> ListSimulatedCitiesAsync(
        Guid worldId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ResourceDeposit>> ListAvailableDepositsAsync(
        City city,
        IReadOnlyCollection<string> resourceCodes,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<NpcActor>> ListActiveMerchantsAsync(
        City city,
        CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

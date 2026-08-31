using RpgWorld.Domain.Actors;
using RpgWorld.Domain.Worlds;
using RpgWorld.Domain.Worlds.Cities;

namespace RpgWorld.Application.Worlds.Cities;

public sealed class CityService(ICityRepository repository) : ICityService
{
    public async Task<CityMasterView> CreateAsync(
        CreateCityRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Territory);
        var world = await repository.GetWorldAsync(request.WorldId, cancellationToken)
            ?? throw new KeyNotFoundException($"World '{request.WorldId}' was not found.");
        var center = world.PositionAt(request.CenterX, request.CenterY);
        var territory = request.Territory
            .Select(cell => world.PositionAt(cell.X, cell.Y))
            .Distinct()
            .ToArray();
        if ((await repository.ListTilesAsync(world.Id, territory, cancellationToken)).Count != territory.Length)
            throw new InvalidOperationException("Every city territory position must have a persisted map tile.");
        if (await repository.TerritoryOverlapsAsync(world.Id, territory, cancellationToken))
            throw new InvalidOperationException("City territory overlaps an existing city.");
        var residentIds = (request.ResidentActorIds ?? []).Distinct().ToArray();
        if (request.InitialPopulation < residentIds.Length)
            throw new ArgumentOutOfRangeException(nameof(request), "Initial population cannot be lower than named residents.");
        var residents = new List<NpcActor>(residentIds.Length);
        foreach (var residentId in residentIds)
        {
            var npc = await repository.GetNpcAsync(residentId, cancellationToken)
                ?? throw new KeyNotFoundException($"NPC '{residentId}' was not found.");
            if (npc.WorldId != world.Id) throw new InvalidOperationException("Every resident must belong to the city world.");
            if (npc.Status == ActorStatus.Dead) throw new InvalidOperationException("A dead NPC cannot become a city resident.");
            if (npc.ResidentCityId is not null) throw new InvalidOperationException($"NPC '{npc.Id}' already resides in a city.");
            residents.Add(npc);
        }

        var city = City.Create(
            world,
            request.Name,
            center,
            territory,
            request.InitialPopulation,
            request.InitialWealth,
            request.FoundedAtUtc,
            request.GoverningFactionId);
        foreach (var resident in residents)
        {
            city.AddResident(resident.Id, request.FoundedAtUtc, increasePopulation: false);
            resident.JoinCity(city, request.FoundedAtUtc);
        }
        repository.Add(city);
        await repository.SaveChangesAsync(cancellationToken);
        return ToView(city);
    }

    public async Task<CityMasterView?> GetAsync(Guid cityId, CancellationToken cancellationToken = default) =>
        await repository.GetAsync(cityId, cancellationToken) is { } city ? ToView(city) : null;

    public async Task<IReadOnlyList<CityMasterView>> ListByWorldAsync(
        Guid worldId,
        CancellationToken cancellationToken = default) =>
        (await repository.ListByWorldAsync(worldId, cancellationToken)).Select(ToView).ToArray();

    public async Task<CityMasterView> AddResidentAsync(
        Guid cityId,
        Guid actorId,
        DateTimeOffset occurredAtUtc,
        CancellationToken cancellationToken = default)
    {
        var city = await RequiredCityAsync(cityId, cancellationToken);
        var npc = await repository.GetNpcAsync(actorId, cancellationToken)
            ?? throw new KeyNotFoundException($"NPC '{actorId}' was not found.");
        if (npc.WorldId != city.WorldId) throw new InvalidOperationException("NPC and city must belong to the same world.");
        if (npc.Status == ActorStatus.Dead) throw new InvalidOperationException("A dead NPC cannot become a city resident.");
        if (npc.ResidentCityId is { } current && current != city.Id)
            throw new InvalidOperationException("NPC already resides in another city.");
        if (npc.ResidentCityId == city.Id) return ToView(city);
        city.AddResident(npc.Id, occurredAtUtc);
        npc.JoinCity(city, occurredAtUtc);
        await repository.SaveChangesAsync(cancellationToken);
        return ToView(city);
    }

    public async Task<CityMasterView> RemoveResidentAsync(
        Guid cityId,
        Guid actorId,
        string reason,
        DateTimeOffset occurredAtUtc,
        CancellationToken cancellationToken = default)
    {
        var city = await RequiredCityAsync(cityId, cancellationToken);
        var npc = await repository.GetNpcAsync(actorId, cancellationToken)
            ?? throw new KeyNotFoundException($"NPC '{actorId}' was not found.");
        if (npc.ResidentCityId != city.Id) throw new InvalidOperationException("NPC is not a resident of this city.");
        city.RemoveResident(actorId, reason, occurredAtUtc);
        npc.LeaveCity(city.Id, occurredAtUtc);
        await repository.SaveChangesAsync(cancellationToken);
        return ToView(city);
    }

    public async Task<CityMasterView> ChangePopulationAsync(
        Guid cityId,
        int delta,
        string reason,
        DateTimeOffset occurredAtUtc,
        CancellationToken cancellationToken = default)
    {
        var city = await RequiredCityAsync(cityId, cancellationToken);
        city.ChangePopulation(delta, reason, occurredAtUtc);
        await repository.SaveChangesAsync(cancellationToken);
        return ToView(city);
    }

    public async Task<CityMasterView> BeginCrisisAsync(
        Guid cityId,
        string reason,
        int severity,
        DateTimeOffset occurredAtUtc,
        CancellationToken cancellationToken = default)
    {
        var city = await RequiredCityAsync(cityId, cancellationToken);
        city.BeginCrisis(reason, severity, occurredAtUtc);
        await repository.SaveChangesAsync(cancellationToken);
        return ToView(city);
    }

    public async Task<CityMasterView> ResolveCrisisAsync(
        Guid cityId,
        string resolution,
        DateTimeOffset occurredAtUtc,
        CancellationToken cancellationToken = default)
    {
        var city = await RequiredCityAsync(cityId, cancellationToken);
        city.ResolveCrisis(resolution, occurredAtUtc);
        await repository.SaveChangesAsync(cancellationToken);
        return ToView(city);
    }

    public async Task<CityMasterView> DestroyAsync(
        Guid cityId,
        string reason,
        DateTimeOffset destroyedAtUtc,
        CancellationToken cancellationToken = default)
    {
        var city = await RequiredCityAsync(cityId, cancellationToken);
        var residents = await repository.ListResidentsAsync(city.Id, cancellationToken);
        city.Destroy(reason, destroyedAtUtc);
        foreach (var resident in residents) resident.LeaveCity(city.Id, destroyedAtUtc);
        await repository.SaveChangesAsync(cancellationToken);
        return ToView(city);
    }

    private async Task<City> RequiredCityAsync(Guid cityId, CancellationToken cancellationToken) =>
        await repository.GetAsync(cityId, cancellationToken)
        ?? throw new KeyNotFoundException($"City '{cityId}' was not found.");

    private static CityMasterView ToView(City city) => new(
        city.Id,
        city.WorldId,
        city.Name,
        city.CenterX,
        city.CenterY,
        city.Status.ToString(),
        city.Population,
        city.Wealth,
        city.GoverningFactionId,
        city.Territory.OrderBy(position => position.Y).ThenBy(position => position.X)
            .Select(position => new CityTerritoryPosition(position.X, position.Y)).ToArray(),
        city.ResidentActorIds.ToArray(),
        city.BuildingIds.ToArray(),
        new Dictionary<string, decimal>(city.ResourceStocks),
        new Dictionary<string, CityResourceMarketSnapshot>(city.ResourceMarkets),
        city.EconomicCycleCount,
        city.LastEconomicCycleAtUtc,
        city.History.ToArray(),
        city.FoundedAtUtc,
        city.DestroyedAtUtc);
}

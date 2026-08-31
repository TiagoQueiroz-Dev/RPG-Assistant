using RpgWorld.Domain.Actors;
using RpgWorld.Domain.Worlds;
using RpgWorld.Domain.Worlds.Cities;
using RpgWorld.Domain.Worlds.Factions;

namespace RpgWorld.Application.Worlds.Factions;

public sealed class FactionService(IFactionRepository repository) : IFactionService
{
    public async Task<FactionMasterView> CreateAsync(
        CreateFactionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var world = await repository.GetWorldAsync(request.WorldId, cancellationToken)
            ?? throw new KeyNotFoundException($"World '{request.WorldId}' was not found.");
        if (!string.IsNullOrWhiteSpace(request.Name) &&
            await repository.NameExistsAsync(world.Id, request.Name.Trim(), cancellationToken))
            throw new InvalidOperationException($"Faction name '{request.Name}' is already used in this world.");
        var leader = await RequiredActorAsync(request.LeaderActorId, cancellationToken);
        ValidateAvailableActor(leader, world.Id);
        var positions = ToPositions(world, request.Territory ?? []);
        await ValidateTerritoryAsync(world, positions, null, cancellationToken);
        var faction = Faction.Create(
            world, request.Name, request.Type, leader.Id, request.InitialWealth,
            request.InitialMilitaryPower, request.CreatedAtUtc);
        if (positions.Length > 0) faction.ClaimTerritory(world, positions, request.CreatedAtUtc);
        leader.JoinFaction(faction.Id, request.CreatedAtUtc);
        repository.Add(faction);
        await repository.SaveChangesAsync(cancellationToken);
        return ToView(faction);
    }

    public async Task<FactionMasterView?> GetAsync(Guid factionId, CancellationToken cancellationToken = default) =>
        await repository.GetAsync(factionId, cancellationToken) is { } faction ? ToView(faction) : null;

    public async Task<IReadOnlyList<FactionMasterView>> ListByWorldAsync(
        Guid worldId,
        CancellationToken cancellationToken = default) =>
        (await repository.ListByWorldAsync(worldId, cancellationToken)).Select(ToView).ToArray();

    public async Task<FactionMasterView> AddMemberAsync(
        Guid factionId,
        Guid actorId,
        DateTimeOffset occurredAtUtc,
        CancellationToken cancellationToken = default)
    {
        var faction = await RequiredFactionAsync(factionId, cancellationToken);
        var actor = await RequiredActorAsync(actorId, cancellationToken);
        ValidateAvailableActor(actor, faction.WorldId, faction.Id);
        if (actor.FactionId == faction.Id && faction.MemberActorIds.Contains(actor.Id)) return ToView(faction);
        faction.AddMember(actor.Id, occurredAtUtc);
        if (actor.FactionId != faction.Id) actor.JoinFaction(faction.Id, occurredAtUtc);
        await repository.SaveChangesAsync(cancellationToken);
        return ToView(faction);
    }

    public async Task<FactionMasterView> RemoveMemberAsync(
        Guid factionId,
        Guid actorId,
        string reason,
        DateTimeOffset occurredAtUtc,
        CancellationToken cancellationToken = default)
    {
        var faction = await RequiredFactionAsync(factionId, cancellationToken);
        var actor = await RequiredActorAsync(actorId, cancellationToken);
        if (actor.FactionId != faction.Id || !faction.MemberActorIds.Contains(actor.Id))
            throw new InvalidOperationException("Actor is not a member of this faction.");
        faction.RemoveMember(actor.Id, reason, occurredAtUtc);
        actor.LeaveFaction(faction.Id, occurredAtUtc);
        await repository.SaveChangesAsync(cancellationToken);
        return ToView(faction);
    }

    public async Task<FactionMasterView> ChangeLeaderAsync(
        Guid factionId,
        Guid newLeaderActorId,
        string reason,
        DateTimeOffset occurredAtUtc,
        CancellationToken cancellationToken = default)
    {
        var faction = await RequiredFactionAsync(factionId, cancellationToken);
        var leader = await RequiredActorAsync(newLeaderActorId, cancellationToken);
        if (leader.WorldId != faction.WorldId || leader.Status == ActorStatus.Dead || leader.FactionId != faction.Id)
            throw new InvalidOperationException("The new leader must be a living member of this faction.");
        faction.ChangeLeader(leader.Id, reason, occurredAtUtc);
        await repository.SaveChangesAsync(cancellationToken);
        return ToView(faction);
    }

    public async Task<FactionMasterView> AssociateCityAsync(
        Guid factionId,
        Guid cityId,
        bool claimCityTerritory,
        DateTimeOffset occurredAtUtc,
        CancellationToken cancellationToken = default)
    {
        var faction = await RequiredFactionAsync(factionId, cancellationToken);
        var city = await repository.GetCityAsync(cityId, cancellationToken)
            ?? throw new KeyNotFoundException($"City '{cityId}' was not found.");
        if (city.WorldId != faction.WorldId) throw new InvalidOperationException("City and faction must belong to the same world.");
        if (city.Status == CityStatus.Destroyed) throw new InvalidOperationException("Destroyed city cannot join a faction.");
        if (city.GoverningFactionId is { } current && current != faction.Id)
            throw new InvalidOperationException("City is already governed by another faction.");
        var world = await repository.GetWorldAsync(faction.WorldId, cancellationToken)
            ?? throw new KeyNotFoundException($"World '{faction.WorldId}' was not found.");
        if (claimCityTerritory)
            await ValidateTerritoryAsync(world, city.Territory.ToArray(), faction.Id, cancellationToken);
        faction.AssociateCity(city.Id, occurredAtUtc);
        if (claimCityTerritory) faction.ClaimTerritory(world, city.Territory, occurredAtUtc);
        city.SetGoverningFaction(faction.Id, occurredAtUtc);
        await repository.SaveChangesAsync(cancellationToken);
        return ToView(faction);
    }

    public async Task<FactionMasterView> ReleaseCityAsync(
        Guid factionId,
        Guid cityId,
        bool releaseCityTerritory,
        string reason,
        DateTimeOffset occurredAtUtc,
        CancellationToken cancellationToken = default)
    {
        var faction = await RequiredFactionAsync(factionId, cancellationToken);
        var city = await repository.GetCityAsync(cityId, cancellationToken)
            ?? throw new KeyNotFoundException($"City '{cityId}' was not found.");
        if (city.GoverningFactionId != faction.Id) throw new InvalidOperationException("City is not governed by this faction.");
        faction.ReleaseCity(city.Id, reason, occurredAtUtc);
        if (releaseCityTerritory) faction.ReleaseTerritory(city.Territory, reason, occurredAtUtc);
        city.SetGoverningFaction(null, occurredAtUtc);
        await repository.SaveChangesAsync(cancellationToken);
        return ToView(faction);
    }

    public async Task<FactionMasterView> ClaimTerritoryAsync(
        Guid factionId,
        IReadOnlyCollection<FactionTerritoryPosition> territory,
        DateTimeOffset occurredAtUtc,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(territory);
        var faction = await RequiredFactionAsync(factionId, cancellationToken);
        var world = await repository.GetWorldAsync(faction.WorldId, cancellationToken)
            ?? throw new KeyNotFoundException($"World '{faction.WorldId}' was not found.");
        var positions = ToPositions(world, territory);
        await ValidateTerritoryAsync(world, positions, faction.Id, cancellationToken);
        faction.ClaimTerritory(world, positions, occurredAtUtc);
        await repository.SaveChangesAsync(cancellationToken);
        return ToView(faction);
    }

    public async Task<FactionMasterView> ReleaseTerritoryAsync(
        Guid factionId,
        IReadOnlyCollection<FactionTerritoryPosition> territory,
        string reason,
        DateTimeOffset occurredAtUtc,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(territory);
        var faction = await RequiredFactionAsync(factionId, cancellationToken);
        var world = await repository.GetWorldAsync(faction.WorldId, cancellationToken)
            ?? throw new KeyNotFoundException($"World '{faction.WorldId}' was not found.");
        faction.ReleaseTerritory(ToPositions(world, territory), reason, occurredAtUtc);
        await repository.SaveChangesAsync(cancellationToken);
        return ToView(faction);
    }

    public Task<FactionMasterView> AdjustWealthAsync(
        Guid factionId, decimal delta, string reason, DateTimeOffset occurredAtUtc,
        CancellationToken cancellationToken = default) =>
        MutateAsync(factionId, faction => faction.AdjustWealth(delta, reason, occurredAtUtc), cancellationToken);

    public Task<FactionMasterView> SetMilitaryPowerAsync(
        Guid factionId, decimal value, string reason, DateTimeOffset occurredAtUtc,
        CancellationToken cancellationToken = default) =>
        MutateAsync(factionId, faction => faction.SetMilitaryPower(value, reason, occurredAtUtc), cancellationToken);

    public async Task<FactionMasterView> ApplyRelationModifierAsync(
        Guid factionId,
        Guid targetFactionId,
        FactionRelationModifier modifier,
        DateTimeOffset occurredAtUtc,
        CancellationToken cancellationToken = default)
    {
        var faction = await RequiredFactionAsync(factionId, cancellationToken);
        var target = await RequiredFactionAsync(targetFactionId, cancellationToken);
        if (target.WorldId != faction.WorldId) throw new InvalidOperationException("Related factions must belong to the same world.");
        if (target.Status == FactionStatus.Dissolved) throw new InvalidOperationException("Cannot change relations with a dissolved faction.");
        faction.ApplyRelationModifier(target.Id, modifier, occurredAtUtc);
        await repository.SaveChangesAsync(cancellationToken);
        return ToView(faction);
    }

    public async Task<FactionMasterView> DissolveAsync(
        Guid factionId,
        string reason,
        DateTimeOffset occurredAtUtc,
        CancellationToken cancellationToken = default)
    {
        var faction = await RequiredFactionAsync(factionId, cancellationToken);
        var members = await repository.ListMembersAsync(faction.Id, cancellationToken);
        var cities = await repository.ListCitiesAsync(faction.Id, cancellationToken);
        faction.Dissolve(reason, occurredAtUtc);
        foreach (var member in members) member.LeaveFaction(faction.Id, occurredAtUtc);
        foreach (var city in cities) city.SetGoverningFaction(null, occurredAtUtc);
        await repository.SaveChangesAsync(cancellationToken);
        return ToView(faction);
    }

    private async Task<FactionMasterView> MutateAsync(
        Guid factionId,
        Action<Faction> mutate,
        CancellationToken cancellationToken)
    {
        var faction = await RequiredFactionAsync(factionId, cancellationToken);
        mutate(faction);
        await repository.SaveChangesAsync(cancellationToken);
        return ToView(faction);
    }

    private async Task ValidateTerritoryAsync(
        World world,
        Position[] positions,
        Guid? excludingFactionId,
        CancellationToken cancellationToken)
    {
        if (positions.Length == 0) return;
        if ((await repository.ListTilesAsync(world.Id, positions, cancellationToken)).Count != positions.Length)
            throw new InvalidOperationException("Every faction territory position must have a persisted map tile.");
        if (await repository.TerritoryOverlapsAsync(world.Id, positions, excludingFactionId, cancellationToken))
            throw new InvalidOperationException("Faction territory overlaps territory controlled by another faction.");
    }

    private static Position[] ToPositions(World world, IEnumerable<FactionTerritoryPosition> territory) =>
        territory.Select(cell => world.PositionAt(cell.X, cell.Y)).Distinct().ToArray();

    private static void ValidateAvailableActor(Actor actor, Guid worldId, Guid? expectedFactionId = null)
    {
        if (actor.WorldId != worldId) throw new InvalidOperationException("Actor and faction must belong to the same world.");
        if (actor.Status == ActorStatus.Dead) throw new InvalidOperationException("A dead actor cannot join or lead a faction.");
        if (actor.FactionId is { } current && current != expectedFactionId)
            throw new InvalidOperationException("Actor already belongs to another faction.");
    }

    private async Task<Faction> RequiredFactionAsync(Guid factionId, CancellationToken cancellationToken) =>
        await repository.GetAsync(factionId, cancellationToken)
        ?? throw new KeyNotFoundException($"Faction '{factionId}' was not found.");

    private async Task<Actor> RequiredActorAsync(Guid actorId, CancellationToken cancellationToken) =>
        await repository.GetActorAsync(actorId, cancellationToken)
        ?? throw new KeyNotFoundException($"Actor '{actorId}' was not found.");

    private static FactionMasterView ToView(Faction faction) => new(
        faction.Id,
        faction.WorldId,
        faction.Name,
        faction.Type.ToString(),
        faction.Status.ToString(),
        faction.LeaderActorId,
        faction.MemberActorIds.ToArray(),
        faction.ControlledCityIds.ToArray(),
        faction.Territory.OrderBy(position => position.Y).ThenBy(position => position.X)
            .Select(position => new FactionTerritoryPosition(position.X, position.Y)).ToArray(),
        faction.Wealth,
        faction.MilitaryPower,
        faction.Relations.Values.OrderBy(relation => relation.TargetFactionId)
            .Select(relation => new FactionRelationView(
                relation.TargetFactionId,
                Faction.StateName(relation.Kind),
                relation.Affinity,
                relation.Tension,
                relation.IsVassal,
                relation.UpdatedAtUtc,
                relation.History.Select(change => new FactionRelationChangeView(
                    change.Id,
                    change.Source.ToString(),
                    change.Reason,
                    change.AffinityDelta,
                    change.TensionDelta,
                    change.PreviousAffinity,
                    change.Affinity,
                    change.PreviousTension,
                    change.Tension,
                    Faction.StateName(change.PreviousState),
                    Faction.StateName(change.State),
                    change.SourceEventId,
                    change.OccurredAtUtc)).ToArray())).ToArray(),
        faction.History.ToArray(),
        faction.CreatedAtUtc,
        faction.DissolvedAtUtc);
}

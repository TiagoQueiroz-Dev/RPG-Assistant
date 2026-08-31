using System.Globalization;
using RpgWorld.Domain.Events;

namespace RpgWorld.Domain.Worlds.Factions;

public enum FactionType { Kingdom, Guild, Cult, BanditGroup, Tribe, Army, MerchantGuild }
public enum FactionStatus { Active, Dissolved }

public sealed class Faction : AggregateRoot
{
    public const int MaximumTerritoryTiles = 100_000;

    private List<Guid> _memberActorIds = [];
    private List<Guid> _controlledCityIds = [];
    private List<FactionTerritoryTile> _territoryTiles = [];
    private Dictionary<Guid, FactionRelation> _relations = [];
    private List<FactionHistoryEntry> _history = [];

    private Faction() { }

    private Faction(
        World world,
        string name,
        FactionType type,
        Guid leaderActorId,
        decimal initialWealth,
        decimal initialMilitaryPower,
        DateTimeOffset createdAtUtc)
    {
        Id = Guid.CreateVersion7();
        WorldId = world.Id;
        Name = RequiredText(name, nameof(name), 200);
        Type = type;
        LeaderActorId = RequiredId(leaderActorId, nameof(leaderActorId));
        _memberActorIds.Add(leaderActorId);
        Wealth = NonNegative(initialWealth, nameof(initialWealth));
        MilitaryPower = NonNegative(initialMilitaryPower, nameof(initialMilitaryPower));
        Status = FactionStatus.Active;
        CreatedAtUtc = createdAtUtc.ToUniversalTime();
        UpdatedAtUtc = CreatedAtUtc;
        AddHistory(FactionHistoryEventTypes.Created, $"Faction {Name} was created.", CreatedAtUtc,
            new Dictionary<string, string> { ["type"] = Type.ToString() });
        RaiseDomainEvent(new FactionCreatedEvent(Id, WorldId, Name, Type.ToString(), leaderActorId, CreatedAtUtc));
    }

    public Guid Id { get; private set; }
    public Guid WorldId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public FactionType Type { get; private set; }
    public FactionStatus Status { get; private set; }
    public Guid? LeaderActorId { get; private set; }
    public decimal Wealth { get; private set; }
    public decimal MilitaryPower { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public DateTimeOffset? DissolvedAtUtc { get; private set; }
    public long Version { get; private set; }
    public IReadOnlyList<Guid> MemberActorIds => _memberActorIds.ToArray();
    public IReadOnlyList<Guid> ControlledCityIds => _controlledCityIds.ToArray();
    public IReadOnlyList<FactionTerritoryTile> TerritoryTiles => _territoryTiles.ToArray();
    public IReadOnlyList<Position> Territory => _territoryTiles
        .Where(tile => tile.IsActive).Select(tile => tile.Position).ToArray();
    public IReadOnlyDictionary<Guid, FactionRelation> Relations =>
        new Dictionary<Guid, FactionRelation>(_relations);
    public IReadOnlyList<FactionHistoryEntry> History => _history
        .Select(entry => entry with { Metadata = new Dictionary<string, string>(entry.Metadata) }).ToArray();

    public static Faction Create(
        World world,
        string name,
        FactionType type,
        Guid leaderActorId,
        decimal initialWealth,
        decimal initialMilitaryPower,
        DateTimeOffset createdAtUtc)
    {
        ArgumentNullException.ThrowIfNull(world);
        if (!Enum.IsDefined(type)) throw new ArgumentOutOfRangeException(nameof(type));
        return new Faction(world, name, type, leaderActorId, initialWealth, initialMilitaryPower, createdAtUtc);
    }

    public bool AddMember(Guid actorId, DateTimeOffset occurredAtUtc)
    {
        EnsureActive();
        RequiredId(actorId, nameof(actorId));
        if (_memberActorIds.Contains(actorId)) return false;
        _memberActorIds.Add(actorId);
        Touch(occurredAtUtc);
        AddHistory(FactionHistoryEventTypes.MemberJoined, $"Actor {actorId} joined the faction.", UpdatedAtUtc,
            new Dictionary<string, string> { ["actorId"] = actorId.ToString() });
        RaiseDomainEvent(new FactionMemberJoinedEvent(Id, WorldId, actorId, UpdatedAtUtc));
        return true;
    }

    public bool RemoveMember(Guid actorId, string reason, DateTimeOffset occurredAtUtc)
    {
        EnsureActive();
        if (actorId == LeaderActorId)
            throw new InvalidOperationException("The current leader cannot leave before leadership changes.");
        var description = RequiredText(reason, nameof(reason), 500);
        if (!_memberActorIds.Remove(actorId)) return false;
        Touch(occurredAtUtc);
        AddHistory(FactionHistoryEventTypes.MemberLeft, description, UpdatedAtUtc,
            new Dictionary<string, string> { ["actorId"] = actorId.ToString() });
        RaiseDomainEvent(new FactionMemberLeftEvent(Id, WorldId, actorId, description, UpdatedAtUtc));
        return true;
    }

    public void ChangeLeader(Guid newLeaderActorId, string reason, DateTimeOffset occurredAtUtc)
    {
        EnsureActive();
        RequiredId(newLeaderActorId, nameof(newLeaderActorId));
        if (!_memberActorIds.Contains(newLeaderActorId))
            throw new InvalidOperationException("The new leader must already be a faction member.");
        if (LeaderActorId == newLeaderActorId) return;
        var description = RequiredText(reason, nameof(reason), 500);
        var previous = LeaderActorId!.Value;
        LeaderActorId = newLeaderActorId;
        Touch(occurredAtUtc);
        AddHistory(FactionHistoryEventTypes.LeaderChanged, description, UpdatedAtUtc,
            new Dictionary<string, string>
            {
                ["previousLeaderActorId"] = previous.ToString(),
                ["newLeaderActorId"] = newLeaderActorId.ToString()
            });
        RaiseDomainEvent(new FactionLeaderChangedEvent(
            Id, WorldId, previous, newLeaderActorId, description, UpdatedAtUtc));
    }

    public bool AssociateCity(Guid cityId, DateTimeOffset occurredAtUtc)
    {
        EnsureActive();
        RequiredId(cityId, nameof(cityId));
        if (_controlledCityIds.Contains(cityId)) return false;
        _controlledCityIds.Add(cityId);
        Touch(occurredAtUtc);
        AddHistory(FactionHistoryEventTypes.CityAssociated, $"City {cityId} joined the faction.", UpdatedAtUtc,
            new Dictionary<string, string> { ["cityId"] = cityId.ToString() });
        return true;
    }

    public bool ReleaseCity(Guid cityId, string reason, DateTimeOffset occurredAtUtc)
    {
        EnsureActive();
        var description = RequiredText(reason, nameof(reason), 500);
        if (!_controlledCityIds.Remove(cityId)) return false;
        Touch(occurredAtUtc);
        AddHistory(FactionHistoryEventTypes.CityReleased, description, UpdatedAtUtc,
            new Dictionary<string, string> { ["cityId"] = cityId.ToString() });
        return true;
    }

    public int ClaimTerritory(World world, IEnumerable<Position> positions, DateTimeOffset occurredAtUtc)
    {
        EnsureActive();
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(positions);
        if (world.Id != WorldId) throw new InvalidOperationException("Territory world must match the faction world.");
        var requested = positions.Distinct().ToArray();
        if (requested.Any(position => !world.Contains(position)))
            throw new ArgumentOutOfRangeException(nameof(positions), "Every territory tile must be inside the faction world.");
        var active = Territory.ToHashSet();
        var added = requested.Where(active.Add).ToArray();
        if (active.Count > MaximumTerritoryTiles)
            throw new ArgumentOutOfRangeException(nameof(positions), $"Territory cannot exceed {MaximumTerritoryTiles} tiles.");
        if (added.Length == 0) return 0;
        _territoryTiles.AddRange(added.Select(position => FactionTerritoryTile.Create(Id, position)));
        Touch(occurredAtUtc);
        AddHistory(FactionHistoryEventTypes.TerritoryClaimed, $"Claimed {added.Length} territory tiles.", UpdatedAtUtc,
            new Dictionary<string, string> { ["tileCount"] = added.Length.ToString(CultureInfo.InvariantCulture) });
        return added.Length;
    }

    public int ReleaseTerritory(IEnumerable<Position> positions, string reason, DateTimeOffset occurredAtUtc)
    {
        EnsureActive();
        ArgumentNullException.ThrowIfNull(positions);
        var description = RequiredText(reason, nameof(reason), 500);
        var requested = positions.ToHashSet();
        var released = _territoryTiles.Where(tile => tile.IsActive && requested.Contains(tile.Position)).ToArray();
        if (released.Length == 0) return 0;
        var instant = occurredAtUtc.ToUniversalTime();
        foreach (var tile in released) tile.Release(instant);
        Touch(instant);
        AddHistory(FactionHistoryEventTypes.TerritoryReleased, description, UpdatedAtUtc,
            new Dictionary<string, string> { ["tileCount"] = released.Length.ToString(CultureInfo.InvariantCulture) });
        return released.Length;
    }

    public void AdjustWealth(decimal delta, string reason, DateTimeOffset occurredAtUtc)
    {
        EnsureActive();
        if (delta == 0m) throw new ArgumentOutOfRangeException(nameof(delta));
        var next = checked(Wealth + delta);
        if (next < 0m) throw new InvalidOperationException("Faction wealth cannot become negative.");
        Wealth = next;
        Touch(occurredAtUtc);
        AddHistory(FactionHistoryEventTypes.PowerChanged, RequiredText(reason, nameof(reason), 500), UpdatedAtUtc,
            new Dictionary<string, string> { ["wealth"] = Wealth.ToString(CultureInfo.InvariantCulture) });
    }

    public void SetMilitaryPower(decimal value, string reason, DateTimeOffset occurredAtUtc)
    {
        EnsureActive();
        MilitaryPower = NonNegative(value, nameof(value));
        Touch(occurredAtUtc);
        AddHistory(FactionHistoryEventTypes.PowerChanged, RequiredText(reason, nameof(reason), 500), UpdatedAtUtc,
            new Dictionary<string, string> { ["militaryPower"] = MilitaryPower.ToString(CultureInfo.InvariantCulture) });
    }

    public FactionRelation ApplyRelationModifier(
        Guid targetFactionId,
        FactionRelationModifier modifier,
        DateTimeOffset occurredAtUtc)
    {
        EnsureActive();
        if (targetFactionId == Guid.Empty || targetFactionId == Id)
            throw new ArgumentException("Related faction is invalid.", nameof(targetFactionId));
        ArgumentNullException.ThrowIfNull(modifier);
        var current = _relations.GetValueOrDefault(targetFactionId)
            ?? FactionRelation.Neutral(targetFactionId, CreatedAtUtc);
        var updated = current.Apply(modifier, occurredAtUtc);
        Touch(occurredAtUtc);
        _relations[targetFactionId] = updated;
        var stateChanged = current.Kind != updated.Kind;
        AddHistory(
            stateChanged ? FactionHistoryEventTypes.DiplomaticStateChanged : FactionHistoryEventTypes.RelationChanged,
            modifier.Reason,
            UpdatedAtUtc,
            new Dictionary<string, string>
            {
                ["targetFactionId"] = targetFactionId.ToString(),
                ["previousState"] = StateName(current.Kind),
                ["state"] = StateName(updated.Kind),
                ["affinity"] = updated.Affinity.ToString(CultureInfo.InvariantCulture),
                ["tension"] = updated.Tension.ToString(CultureInfo.InvariantCulture),
                ["source"] = modifier.Source.ToString()
            });
        if (stateChanged)
            RaiseDomainEvent(new FactionDiplomaticStateChangedEvent(
                Id, targetFactionId, WorldId, current.Kind, updated.Kind,
                updated.Affinity, updated.Tension, modifier.Reason, modifier.SourceEventId, UpdatedAtUtc));
        return updated;
    }

    public FactionRelation RecordWarAssessment(
        Guid targetFactionId,
        FactionWarScore score,
        DateTimeOffset occurredAtUtc)
    {
        EnsureActive();
        if (targetFactionId == Guid.Empty || targetFactionId == Id)
            throw new ArgumentException("War target faction is invalid.", nameof(targetFactionId));
        ArgumentNullException.ThrowIfNull(score);
        var relation = _relations.GetValueOrDefault(targetFactionId)
            ?? FactionRelation.Neutral(targetFactionId, CreatedAtUtc);
        var updated = relation.RecordWarScore(score);
        _relations[targetFactionId] = updated;
        Touch(occurredAtUtc);
        return updated;
    }

    public void PreventWar(
        Guid targetFactionId,
        DateTimeOffset preventedUntilUtc,
        string reason,
        DateTimeOffset occurredAtUtc)
    {
        EnsureActive();
        if (targetFactionId == Guid.Empty || targetFactionId == Id)
            throw new ArgumentException("War target faction is invalid.", nameof(targetFactionId));
        var relation = _relations.GetValueOrDefault(targetFactionId)
            ?? FactionRelation.Neutral(targetFactionId, CreatedAtUtc);
        var updated = relation.PreventWar(preventedUntilUtc, reason, occurredAtUtc);
        _relations[targetFactionId] = updated;
        Touch(occurredAtUtc);
        AddHistory(FactionHistoryEventTypes.WarPrevented, reason, UpdatedAtUtc,
            new Dictionary<string, string>
            {
                ["targetFactionId"] = targetFactionId.ToString(),
                ["preventedUntilUtc"] = preventedUntilUtc.ToUniversalTime().ToString("O")
            });
    }

    public void AllowWar(Guid targetFactionId, string reason, DateTimeOffset occurredAtUtc)
    {
        EnsureActive();
        if (!_relations.TryGetValue(targetFactionId, out var relation)) return;
        _relations[targetFactionId] = relation.AllowWar(occurredAtUtc);
        Touch(occurredAtUtc);
        AddHistory(FactionHistoryEventTypes.WarPreventionLifted, RequiredText(reason, nameof(reason), 500), UpdatedAtUtc,
            new Dictionary<string, string> { ["targetFactionId"] = targetFactionId.ToString() });
    }

    public bool DeclareWar(
        Guid targetFactionId,
        FactionWarScore warScore,
        string reason,
        bool forcedByGameMaster,
        DateTimeOffset occurredAtUtc)
    {
        EnsureActive();
        if (targetFactionId == Guid.Empty || targetFactionId == Id)
            throw new ArgumentException("War target faction is invalid.", nameof(targetFactionId));
        ArgumentNullException.ThrowIfNull(warScore);
        var current = _relations.GetValueOrDefault(targetFactionId)
            ?? FactionRelation.Neutral(targetFactionId, CreatedAtUtc);
        if (current.Kind == FactionRelationKind.War) return false;
        if (!forcedByGameMaster && current.IsWarPreventedAt(occurredAtUtc)) return false;
        var description = RequiredText(reason, nameof(reason), 500);
        var tensionDelta = Math.Max(0, 80 - current.Tension);
        var relation = ApplyRelationModifier(
            targetFactionId,
            new FactionRelationModifier(
                FactionRelationModifierSource.Event,
                description,
                tensionDelta: tensionDelta,
                vassalage: false),
            occurredAtUtc);
        relation = relation.RecordWarScore(warScore);
        _relations[targetFactionId] = relation;
        AddHistory(FactionHistoryEventTypes.WarDeclared, description, occurredAtUtc,
            new Dictionary<string, string>
            {
                ["targetFactionId"] = targetFactionId.ToString(),
                ["warScore"] = warScore.Total.ToString(CultureInfo.InvariantCulture),
                ["threshold"] = warScore.DeclareWarThreshold.ToString(CultureInfo.InvariantCulture),
                ["forcedByGameMaster"] = forcedByGameMaster.ToString()
            });
        RaiseDomainEvent(new FactionWarDeclaredEvent(
            Id, targetFactionId, WorldId, warScore, forcedByGameMaster, description,
            occurredAtUtc.ToUniversalTime()));
        return true;
    }

    public void Dissolve(string reason, DateTimeOffset occurredAtUtc)
    {
        EnsureActive();
        var description = RequiredText(reason, nameof(reason), 500);
        var instant = occurredAtUtc.ToUniversalTime();
        var formerLeader = LeaderActorId;
        var formerMembers = string.Join(',', _memberActorIds);
        var formerCities = string.Join(',', _controlledCityIds);
        foreach (var tile in _territoryTiles.Where(tile => tile.IsActive)) tile.Release(instant);
        _memberActorIds.Clear();
        _controlledCityIds.Clear();
        LeaderActorId = null;
        Status = FactionStatus.Dissolved;
        DissolvedAtUtc = instant;
        Touch(instant);
        AddHistory(FactionHistoryEventTypes.Dissolved, description, instant,
            new Dictionary<string, string>
            {
                ["formerLeaderActorId"] = formerLeader?.ToString() ?? string.Empty,
                ["formerMemberActorIds"] = formerMembers,
                ["formerCityIds"] = formerCities
            });
        RaiseDomainEvent(new FactionDissolvedEvent(Id, WorldId, description, instant));
    }

    private void EnsureActive()
    {
        if (Status == FactionStatus.Dissolved) throw new InvalidOperationException("Dissolved faction cannot change.");
    }

    private void Touch(DateTimeOffset occurredAtUtc)
    {
        var instant = occurredAtUtc.ToUniversalTime();
        if (instant < UpdatedAtUtc) throw new ArgumentOutOfRangeException(nameof(occurredAtUtc), "Faction time cannot move backwards.");
        UpdatedAtUtc = instant;
        Version = checked(Version + 1);
    }

    private void AddHistory(
        string eventType,
        string description,
        DateTimeOffset occurredAtUtc,
        Dictionary<string, string>? metadata = null) =>
        _history.Add(new FactionHistoryEntry(
            Guid.CreateVersion7(), eventType, description, LeaderActorId, _memberActorIds.Count,
            occurredAtUtc.ToUniversalTime(), metadata ?? []));

    private static Guid RequiredId(Guid id, string parameterName) =>
        id == Guid.Empty ? throw new ArgumentException("Identifier cannot be empty.", parameterName) : id;

    private static decimal NonNegative(decimal value, string parameterName) =>
        value < 0m ? throw new ArgumentOutOfRangeException(parameterName) : value;

    private static string RequiredText(string value, string parameterName, int maximumLength)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("Text cannot be empty.", parameterName);
        var normalized = value.Trim();
        if (normalized.Length > maximumLength)
            throw new ArgumentException($"Text cannot exceed {maximumLength} characters.", parameterName);
        return normalized;
    }

    public static string StateName(FactionRelationKind state) => state switch
    {
        FactionRelationKind.Alliance => nameof(FactionRelationKind.Alliance),
        FactionRelationKind.Neutral => nameof(FactionRelationKind.Neutral),
        FactionRelationKind.Hostile => nameof(FactionRelationKind.Hostile),
        FactionRelationKind.War => nameof(FactionRelationKind.War),
        FactionRelationKind.Vassal => nameof(FactionRelationKind.Vassal),
        _ => throw new ArgumentOutOfRangeException(nameof(state))
    };
}

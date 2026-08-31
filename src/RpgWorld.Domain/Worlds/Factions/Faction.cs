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

    public void SetRelation(
        Guid targetFactionId,
        FactionRelationKind kind,
        int score,
        string reason,
        DateTimeOffset occurredAtUtc)
    {
        EnsureActive();
        if (targetFactionId == Guid.Empty || targetFactionId == Id)
            throw new ArgumentException("Related faction is invalid.", nameof(targetFactionId));
        if (!Enum.IsDefined(kind)) throw new ArgumentOutOfRangeException(nameof(kind));
        if (score is < -100 or > 100) throw new ArgumentOutOfRangeException(nameof(score));
        var description = RequiredText(reason, nameof(reason), 500);
        Touch(occurredAtUtc);
        _relations[targetFactionId] = new FactionRelation(targetFactionId, kind, score, UpdatedAtUtc);
        AddHistory(FactionHistoryEventTypes.RelationChanged, description, UpdatedAtUtc,
            new Dictionary<string, string>
            {
                ["targetFactionId"] = targetFactionId.ToString(),
                ["kind"] = kind.ToString(),
                ["score"] = score.ToString(CultureInfo.InvariantCulture)
            });
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
}

using RpgWorld.Domain.Events;
using RpgWorld.Domain.Worlds;

namespace RpgWorld.Domain.Actors;

public abstract class Actor : AggregateRoot
{
    private Dictionary<string, int> _attributes = new(StringComparer.OrdinalIgnoreCase);
    private List<InventoryItem> _inventory = [];
    private Dictionary<Guid, int> _reputation = [];
    private List<ActorRelationship> _relationships = [];

    protected Actor() { }

    protected Actor(
        string name,
        World world,
        Position position,
        int maximumHealth,
        DateTimeOffset createdAtUtc)
    {
        ArgumentNullException.ThrowIfNull(world);
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Actor name is required.", nameof(name));
        if (!world.Contains(position)) throw new ArgumentOutOfRangeException(nameof(position), "Actor position must be inside its world.");
        if (maximumHealth <= 0) throw new ArgumentOutOfRangeException(nameof(maximumHealth));
        Id = Guid.CreateVersion7();
        Name = name.Trim();
        WorldId = world.Id;
        X = position.X;
        Y = position.Y;
        MaximumHealth = maximumHealth;
        Health = maximumHealth;
        Status = ActorStatus.Active;
        CreatedAtUtc = createdAtUtc.ToUniversalTime();
        UpdatedAtUtc = CreatedAtUtc;
    }

    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public Guid WorldId { get; private set; }
    public int X { get; private set; }
    public int Y { get; private set; }
    public Position Position => new(WorldId, X, Y);
    public int Health { get; private set; }
    public int MaximumHealth { get; private set; }
    public Guid? FactionId { get; private set; }
    public ActorStatus Status { get; private set; }
    public string? CurrentAction { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public IReadOnlyDictionary<string, int> Attributes => _attributes;
    public IReadOnlyList<InventoryItem> Inventory => _inventory;
    public IReadOnlyDictionary<Guid, int> Reputation => _reputation;
    public IReadOnlyList<ActorRelationship> Relationships => _relationships;
    public abstract string Kind { get; }

    protected void RecordCreation(DateTimeOffset occurredAtUtc) =>
        RaiseDomainEvent(new ActorCreatedEvent(Id, WorldId, Kind, Position, occurredAtUtc));

    public void Move(World world, Position destination, DateTimeOffset occurredAtUtc)
    {
        EnsureAlive();
        ArgumentNullException.ThrowIfNull(world);
        if (world.Id != WorldId || !world.Contains(destination))
            throw new ArgumentOutOfRangeException(nameof(destination), "Destination must be inside the actor's world.");
        if (destination == Position) return;
        var origin = Position;
        X = destination.X;
        Y = destination.Y;
        Touch(occurredAtUtc);
        RaiseDomainEvent(new ActorMovedEvent(Id, WorldId, origin, destination, occurredAtUtc));
    }

    public void TakeDamage(int amount, Guid? sourceActorId, DateTimeOffset occurredAtUtc)
    {
        EnsureAlive();
        if (amount <= 0) throw new ArgumentOutOfRangeException(nameof(amount));
        if (sourceActorId == Guid.Empty) throw new ArgumentException("Source actor cannot be empty.", nameof(sourceActorId));
        var applied = Math.Min(amount, Health);
        Health -= applied;
        Status = Health == 0 ? ActorStatus.Dead : ActorStatus.Active;
        CurrentAction = null;
        Touch(occurredAtUtc);
        RaiseDomainEvent(new ActorDamagedEvent(Id, sourceActorId, WorldId, applied, Health, occurredAtUtc));
        if (Health == 0) RaiseDomainEvent(new ActorKilledEvent(Id, sourceActorId, WorldId, occurredAtUtc));
    }

    public void SetCurrentAction(string? action, DateTimeOffset occurredAtUtc)
    {
        EnsureAlive();
        if (action is { Length: > 120 }) throw new ArgumentException("Current action is too long.", nameof(action));
        CurrentAction = string.IsNullOrWhiteSpace(action) ? null : action.Trim();
        Touch(occurredAtUtc);
    }

    public void SetAttribute(string code, int value, DateTimeOffset occurredAtUtc)
    {
        EnsureAlive();
        if (string.IsNullOrWhiteSpace(code)) throw new ArgumentException("Attribute code is required.", nameof(code));
        _attributes[code.Trim()] = value;
        Touch(occurredAtUtc);
    }

    public void AddInventory(string itemCode, int quantity, DateTimeOffset occurredAtUtc)
    {
        EnsureAlive();
        var item = new InventoryItem(itemCode, quantity);
        var existing = _inventory.FindIndex(entry => string.Equals(entry.ItemCode, item.ItemCode, StringComparison.OrdinalIgnoreCase));
        if (existing >= 0) _inventory[existing] = _inventory[existing] with { Quantity = checked(_inventory[existing].Quantity + quantity) };
        else _inventory.Add(item);
        Touch(occurredAtUtc);
    }

    public void JoinFaction(Guid? factionId, DateTimeOffset occurredAtUtc)
    {
        EnsureAlive();
        if (factionId == Guid.Empty) throw new ArgumentException("Faction cannot be empty.", nameof(factionId));
        FactionId = factionId;
        Touch(occurredAtUtc);
    }

    public void SetReputation(Guid factionId, int value, DateTimeOffset occurredAtUtc)
    {
        EnsureAlive();
        if (factionId == Guid.Empty) throw new ArgumentException("Faction is required.", nameof(factionId));
        if (value is < -100 or > 100) throw new ArgumentOutOfRangeException(nameof(value));
        _reputation[factionId] = value;
        Touch(occurredAtUtc);
    }

    public void SetRelationship(Guid actorId, string kind, int affinity, DateTimeOffset occurredAtUtc)
    {
        EnsureAlive();
        if (actorId == Id) throw new ArgumentException("Actor cannot relate to itself.", nameof(actorId));
        var relationship = new ActorRelationship(actorId, kind, affinity);
        _relationships.RemoveAll(entry => entry.ActorId == actorId && string.Equals(entry.Kind, relationship.Kind, StringComparison.OrdinalIgnoreCase));
        _relationships.Add(relationship);
        Touch(occurredAtUtc);
    }

    private void EnsureAlive()
    {
        if (Status == ActorStatus.Dead) throw new InvalidOperationException("Dead actors cannot perform actions.");
    }

    private void Touch(DateTimeOffset occurredAtUtc) => UpdatedAtUtc = occurredAtUtc.ToUniversalTime();
}

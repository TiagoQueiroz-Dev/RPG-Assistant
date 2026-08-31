using RpgWorld.Domain.Events;
using RpgWorld.Domain.Worlds.Definitions;

namespace RpgWorld.Domain.Worlds.Resources;

public enum ResourceDepositScope { Tile, Region }

public enum ResourceConsumerKind { Actor, Construction, City }

public readonly record struct ResourceConsumer
{
    public ResourceConsumer(ResourceConsumerKind kind, Guid id)
    {
        if (!Enum.IsDefined(kind)) throw new ArgumentOutOfRangeException(nameof(kind));
        if (id == Guid.Empty) throw new ArgumentException("Consumer identifier cannot be empty.", nameof(id));
        Kind = kind;
        Id = id;
    }

    public ResourceConsumerKind Kind { get; }
    public Guid Id { get; }

    public static ResourceConsumer Actor(Guid id) => new(ResourceConsumerKind.Actor, id);
    public static ResourceConsumer Construction(Guid id) => new(ResourceConsumerKind.Construction, id);
    public static ResourceConsumer City(Guid id) => new(ResourceConsumerKind.City, id);
}

public sealed record ResourceExtraction(
    Guid DepositId,
    string ResourceCode,
    decimal Quantity,
    ResourceConsumer Consumer,
    bool Exhausted);

public sealed class ResourceDeposit : AggregateRoot
{
    private ResourceDeposit() { }

    private ResourceDeposit(
        World world,
        ResourceDefinition definition,
        ResourceDepositScope scope,
        Guid? tileId,
        int regionX,
        int regionY,
        decimal capacity,
        decimal initialQuantity,
        decimal regenerationPerWorldHour,
        Guid? sourceWorldEventId,
        DateTimeOffset spawnedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(definition);
        if (capacity <= 0m) throw new ArgumentOutOfRangeException(nameof(capacity));
        if (initialQuantity is < 0m || initialQuantity > capacity)
            throw new ArgumentOutOfRangeException(nameof(initialQuantity), "Initial quantity must be between zero and capacity.");
        if (regenerationPerWorldHour < 0m) throw new ArgumentOutOfRangeException(nameof(regenerationPerWorldHour));
        if (sourceWorldEventId == Guid.Empty) throw new ArgumentException("World event identifier cannot be empty.", nameof(sourceWorldEventId));
        if (!Enum.IsDefined(scope)) throw new ArgumentOutOfRangeException(nameof(scope));

        Id = Guid.CreateVersion7();
        WorldId = world.Id;
        ResourceCode = definition.Code;
        InventoryItemCode = definition.InventoryItemCode;
        Scope = scope;
        TileId = tileId;
        RegionX = regionX;
        RegionY = regionY;
        Capacity = capacity;
        Quantity = initialQuantity;
        RegenerationPerWorldHour = regenerationPerWorldHour;
        SourceWorldEventId = sourceWorldEventId;
        CreatedAtUtc = spawnedAtUtc.ToUniversalTime();
        UpdatedAtUtc = CreatedAtUtc;
        LastRegeneratedAtUtc = CreatedAtUtc;
        RaiseDomainEvent(new ResourceSpawnedEvent(
            Id, WorldId, ResourceCode, Scope, SourceWorldEventId, CreatedAtUtc));
    }

    public Guid Id { get; private set; }
    public Guid WorldId { get; private set; }
    public string ResourceCode { get; private set; } = string.Empty;
    public string InventoryItemCode { get; private set; } = string.Empty;
    public ResourceDepositScope Scope { get; private set; }
    public Guid? TileId { get; private set; }
    public int RegionX { get; private set; }
    public int RegionY { get; private set; }
    public ChunkCoordinate Region => new(RegionX, RegionY);
    public decimal Quantity { get; private set; }
    public decimal Capacity { get; private set; }
    public decimal RegenerationPerWorldHour { get; private set; }
    public bool IsRenewable => RegenerationPerWorldHour > 0m;
    public bool IsExhausted => Quantity == 0m;
    public bool IsDiscovered { get; private set; }
    public Guid? DiscoveredByActorId { get; private set; }
    public DateTimeOffset? DiscoveredAtUtc { get; private set; }
    public ResourceConsumerKind? LastConsumerKind { get; private set; }
    public Guid? LastConsumerId { get; private set; }
    public Guid? SourceWorldEventId { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public DateTimeOffset LastRegeneratedAtUtc { get; private set; }
    public long Version { get; private set; }

    public static ResourceDeposit SpawnOnTile(
        World world,
        Tile tile,
        ResourceDefinition definition,
        DateTimeOffset spawnedAtUtc,
        decimal? initialQuantity = null,
        decimal? capacity = null,
        decimal? regenerationPerWorldHour = null,
        Guid? sourceWorldEventId = null)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(tile);
        ArgumentNullException.ThrowIfNull(definition);
        if (tile.WorldId != world.Id) throw new ArgumentException("Tile must belong to the resource world.", nameof(tile));
        if (tile.ResourceDepositId is not null) throw new InvalidOperationException("Tile already has a resource deposit.");
        var effectiveCapacity = capacity ?? definition.DefaultCapacity;
        var deposit = new ResourceDeposit(
            world,
            definition,
            ResourceDepositScope.Tile,
            tile.Id,
            world.ChunkAt(tile.Position).X,
            world.ChunkAt(tile.Position).Y,
            effectiveCapacity,
            initialQuantity ?? effectiveCapacity,
            regenerationPerWorldHour ?? definition.RegenerationPerWorldHour,
            sourceWorldEventId,
            spawnedAtUtc);
        tile.AssignResource(deposit.Id);
        return deposit;
    }

    public static ResourceDeposit SpawnInRegion(
        World world,
        ChunkCoordinate region,
        ResourceDefinition definition,
        DateTimeOffset spawnedAtUtc,
        decimal? initialQuantity = null,
        decimal? capacity = null,
        decimal? regenerationPerWorldHour = null,
        Guid? sourceWorldEventId = null)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(definition);
        if (region.X >= world.ChunkColumns || region.Y >= world.ChunkRows)
            throw new ArgumentOutOfRangeException(nameof(region), "Region is outside the world.");
        var effectiveCapacity = capacity ?? definition.DefaultCapacity;
        return new ResourceDeposit(
            world,
            definition,
            ResourceDepositScope.Region,
            tileId: null,
            region.X,
            region.Y,
            effectiveCapacity,
            initialQuantity ?? effectiveCapacity,
            regenerationPerWorldHour ?? definition.RegenerationPerWorldHour,
            sourceWorldEventId,
            spawnedAtUtc);
    }

    public bool Discover(Guid actorId, DateTimeOffset discoveredAtUtc)
    {
        if (actorId == Guid.Empty) throw new ArgumentException("Actor identifier cannot be empty.", nameof(actorId));
        if (IsDiscovered) return false;
        if (discoveredAtUtc.ToUniversalTime() < CreatedAtUtc)
            throw new ArgumentOutOfRangeException(nameof(discoveredAtUtc), "Discovery cannot predate the resource deposit.");
        IsDiscovered = true;
        DiscoveredByActorId = actorId;
        DiscoveredAtUtc = discoveredAtUtc.ToUniversalTime();
        UpdatedAtUtc = DiscoveredAtUtc.Value;
        AdvanceVersion();
        RaiseDomainEvent(new ResourceDiscoveredEvent(Id, actorId, WorldId, DiscoveredAtUtc.Value));
        return true;
    }

    public ResourceExtraction Extract(decimal requestedQuantity, ResourceConsumer consumer, DateTimeOffset extractedAtUtc)
    {
        if (!IsDiscovered) throw new InvalidOperationException("Resource must be discovered before extraction.");
        if (requestedQuantity <= 0m) throw new ArgumentOutOfRangeException(nameof(requestedQuantity));
        if (consumer.Id == Guid.Empty) throw new ArgumentException("Consumer identifier cannot be empty.", nameof(consumer));
        RegenerateTo(extractedAtUtc);
        if (Quantity == 0m) throw new InvalidOperationException("Resource deposit is exhausted.");
        var extracted = Math.Min(requestedQuantity, Quantity);
        Quantity -= extracted;
        LastConsumerKind = consumer.Kind;
        LastConsumerId = consumer.Id;
        UpdatedAtUtc = extractedAtUtc.ToUniversalTime();
        AdvanceVersion();
        if (Quantity == 0m)
        {
            RaiseDomainEvent(new ResourceExhaustedEvent(
                Id, WorldId, ResourceCode, consumer.Kind, consumer.Id, UpdatedAtUtc));
        }
        return new ResourceExtraction(Id, ResourceCode, extracted, consumer, IsExhausted);
    }

    public decimal RegenerateTo(DateTimeOffset worldInstant)
    {
        var instant = worldInstant.ToUniversalTime();
        if (instant < LastRegeneratedAtUtc)
            throw new ArgumentOutOfRangeException(nameof(worldInstant), "Resource time cannot move backwards.");
        var previous = Quantity;
        var previousInstant = LastRegeneratedAtUtc;
        if (RegenerationPerWorldHour > 0m && Quantity < Capacity)
        {
            var elapsedHours = (decimal)(instant - LastRegeneratedAtUtc).TotalHours;
            Quantity = Math.Min(Capacity, Quantity + elapsedHours * RegenerationPerWorldHour);
        }
        LastRegeneratedAtUtc = instant;
        if (Quantity != previous) UpdatedAtUtc = instant;
        if (instant != previousInstant) AdvanceVersion();
        return Quantity - previous;
    }

    public void AdjustQuantity(decimal delta, DateTimeOffset occurredAtUtc)
    {
        if (delta == 0m) throw new ArgumentOutOfRangeException(nameof(delta));
        var instant = occurredAtUtc.ToUniversalTime();
        if (instant < UpdatedAtUtc)
            throw new ArgumentOutOfRangeException(nameof(occurredAtUtc), "Resource adjustment cannot move backwards in time.");
        var next = checked(Quantity + delta);
        if (next is < 0m || next > Capacity)
            throw new ArgumentOutOfRangeException(nameof(delta), "Adjusted quantity must remain between zero and capacity.");
        Quantity = next;
        UpdatedAtUtc = instant;
        LastRegeneratedAtUtc = instant;
        AdvanceVersion();
    }

    private void AdvanceVersion() => Version = checked(Version + 1);
}

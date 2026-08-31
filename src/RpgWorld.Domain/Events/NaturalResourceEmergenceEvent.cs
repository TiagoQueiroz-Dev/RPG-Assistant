using RpgWorld.Domain.Worlds.Resources;

namespace RpgWorld.Domain.Events;

public sealed record NaturalResourceEmergenceEvent : DomainEvent
{
    public NaturalResourceEmergenceEvent(
        Guid worldId,
        string resourceCode,
        ResourceDepositScope scope,
        int x,
        int y,
        DateTimeOffset occurredAtUtc,
        decimal? initialQuantity = null,
        decimal? capacity = null,
        decimal? regenerationPerWorldHour = null)
        : base(occurredAtUtc.ToUniversalTime())
    {
        if (worldId == Guid.Empty) throw new ArgumentException("World identifier cannot be empty.", nameof(worldId));
        if (string.IsNullOrWhiteSpace(resourceCode)) throw new ArgumentException("Resource code is required.", nameof(resourceCode));
        if (!Enum.IsDefined(scope)) throw new ArgumentOutOfRangeException(nameof(scope));
        if (x < 0 || y < 0) throw new ArgumentOutOfRangeException(nameof(x), "Resource coordinates cannot be negative.");
        if (initialQuantity < 0m) throw new ArgumentOutOfRangeException(nameof(initialQuantity));
        if (capacity <= 0m) throw new ArgumentOutOfRangeException(nameof(capacity));
        if (initialQuantity.HasValue && capacity.HasValue && initialQuantity > capacity)
            throw new ArgumentOutOfRangeException(nameof(initialQuantity), "Initial quantity cannot exceed capacity.");
        if (regenerationPerWorldHour < 0m) throw new ArgumentOutOfRangeException(nameof(regenerationPerWorldHour));
        WorldId = worldId;
        ResourceCode = resourceCode.Trim().ToLowerInvariant();
        Scope = scope;
        X = x;
        Y = y;
        InitialQuantity = initialQuantity;
        Capacity = capacity;
        RegenerationPerWorldHour = regenerationPerWorldHour;
    }

    public Guid WorldId { get; }
    public string ResourceCode { get; }
    public ResourceDepositScope Scope { get; }
    public int X { get; }
    public int Y { get; }
    public decimal? InitialQuantity { get; }
    public decimal? Capacity { get; }
    public decimal? RegenerationPerWorldHour { get; }
}

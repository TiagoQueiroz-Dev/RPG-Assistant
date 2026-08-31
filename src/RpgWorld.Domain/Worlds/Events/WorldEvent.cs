using System.Text.Json;

namespace RpgWorld.Domain.Worlds.Events;

public sealed record WorldEventPosition(int X, int Y);

public sealed class WorldEvent
{
    public const int MaximumActorCount = 100;
    public const int MaximumPayloadLength = 65_536;

    private List<Guid> _actorIds = [];
    private WorldEvent() { }

    private WorldEvent(
        Guid id,
        Guid worldId,
        string type,
        DateTimeOffset timestampUtc,
        int? positionX,
        int? positionY,
        IEnumerable<Guid> actorIds,
        string payload,
        int payloadVersion)
    {
        if (id == Guid.Empty) throw new ArgumentException("Event identifier is required.", nameof(id));
        if (worldId == Guid.Empty) throw new ArgumentException("World identifier is required.", nameof(worldId));
        if (string.IsNullOrWhiteSpace(type) || type.Trim().Length > 160)
            throw new ArgumentException("Event type is required and cannot exceed 160 characters.", nameof(type));
        if (positionX < 0 || positionY < 0 || positionX.HasValue != positionY.HasValue)
            throw new ArgumentException("Event position requires valid X and Y coordinates.", nameof(positionX));
        var actors = actorIds.Distinct().ToArray();
        if (actors.Any(actorId => actorId == Guid.Empty) || actors.Length > MaximumActorCount)
            throw new ArgumentException($"Events support up to {MaximumActorCount} valid actors.", nameof(actorIds));
        if (string.IsNullOrWhiteSpace(payload) || payload.Length > MaximumPayloadLength)
            throw new ArgumentException($"Payload is required and cannot exceed {MaximumPayloadLength} characters.", nameof(payload));
        using var _ = JsonDocument.Parse(payload);
        if (payloadVersion <= 0) throw new ArgumentOutOfRangeException(nameof(payloadVersion));
        Id = id;
        WorldId = worldId;
        Type = type.Trim();
        TimestampUtc = timestampUtc.ToUniversalTime();
        PositionX = positionX;
        PositionY = positionY;
        _actorIds = actors.ToList();
        Payload = payload;
        PayloadVersion = payloadVersion;
    }

    public Guid Id { get; private set; }
    public Guid WorldId { get; private set; }
    public string Type { get; private set; } = string.Empty;
    public DateTimeOffset TimestampUtc { get; private set; }
    public int? PositionX { get; private set; }
    public int? PositionY { get; private set; }
    public WorldEventPosition? Position => PositionX.HasValue ? new(PositionX.Value, PositionY!.Value) : null;
    public IReadOnlyList<Guid> ActorIds => _actorIds.ToArray();
    public string Payload { get; private set; } = "{}";
    public int PayloadVersion { get; private set; }

    public static WorldEvent Create(
        Guid id,
        Guid worldId,
        string type,
        DateTimeOffset timestampUtc,
        WorldEventPosition? position,
        IEnumerable<Guid>? actorIds,
        string payload,
        int payloadVersion = 1) =>
        new(id, worldId, type, timestampUtc, position?.X, position?.Y, actorIds ?? [], payload, payloadVersion);
}

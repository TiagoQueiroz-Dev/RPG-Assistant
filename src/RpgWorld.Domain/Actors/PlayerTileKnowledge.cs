using RpgWorld.Domain.Worlds;

namespace RpgWorld.Domain.Actors;

public enum PlayerKnowledgeState
{
    Unknown,
    Discovered,
    Known,
    Visible
}

public sealed class PlayerTileKnowledge
{
    private PlayerTileKnowledge() { }

    private PlayerTileKnowledge(
        Guid playerActorId,
        Position position,
        PlayerKnowledgeState historicalState,
        DateTimeOffset observedAtUtc)
    {
        if (playerActorId == Guid.Empty) throw new ArgumentException("Player actor identifier is required.", nameof(playerActorId));
        if (historicalState is not (PlayerKnowledgeState.Discovered or PlayerKnowledgeState.Known))
            throw new ArgumentOutOfRangeException(nameof(historicalState));
        Id = Guid.CreateVersion7();
        PlayerActorId = playerActorId;
        WorldId = position.WorldId;
        X = position.X;
        Y = position.Y;
        HistoricalState = historicalState;
        DiscoveredAtUtc = observedAtUtc.ToUniversalTime();
        KnownAtUtc = historicalState == PlayerKnowledgeState.Known ? DiscoveredAtUtc : null;
        LastVisibleAtUtc = DiscoveredAtUtc;
    }

    public Guid Id { get; private set; }
    public Guid PlayerActorId { get; private set; }
    public Guid WorldId { get; private set; }
    public int X { get; private set; }
    public int Y { get; private set; }
    public PlayerKnowledgeState HistoricalState { get; private set; }
    public DateTimeOffset DiscoveredAtUtc { get; private set; }
    public DateTimeOffset? KnownAtUtc { get; private set; }
    public DateTimeOffset LastVisibleAtUtc { get; private set; }
    public long Version { get; private set; }
    public Position Position => new(WorldId, X, Y);

    public static PlayerTileKnowledge Discover(
        Guid playerActorId,
        Position position,
        bool known,
        DateTimeOffset observedAtUtc) =>
        new(playerActorId, position, known ? PlayerKnowledgeState.Known : PlayerKnowledgeState.Discovered, observedAtUtc);

    public void Observe(bool known, DateTimeOffset observedAtUtc)
    {
        var instant = observedAtUtc.ToUniversalTime();
        if (instant < LastVisibleAtUtc)
            throw new ArgumentOutOfRangeException(nameof(observedAtUtc), "Visibility cannot move backwards in time.");
        if (known && HistoricalState == PlayerKnowledgeState.Discovered)
        {
            HistoricalState = PlayerKnowledgeState.Known;
            KnownAtUtc = instant;
        }
        LastVisibleAtUtc = instant;
        Version = checked(Version + 1);
    }

    public PlayerKnowledgeState CurrentState(bool visible) =>
        visible ? PlayerKnowledgeState.Visible : HistoricalState;
}

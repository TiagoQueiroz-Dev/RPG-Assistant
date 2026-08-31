namespace RpgWorld.Domain.Actors;

public sealed record ActorRelationship
{
    public ActorRelationship(Guid actorId, string kind, int affinity)
    {
        if (actorId == Guid.Empty) throw new ArgumentException("Related actor is required.", nameof(actorId));
        if (string.IsNullOrWhiteSpace(kind)) throw new ArgumentException("Relationship kind is required.", nameof(kind));
        if (affinity is < -100 or > 100) throw new ArgumentOutOfRangeException(nameof(affinity));
        ActorId = actorId;
        Kind = kind.Trim();
        Affinity = affinity;
    }

    public Guid ActorId { get; init; }
    public string Kind { get; init; }
    public int Affinity { get; init; }
}

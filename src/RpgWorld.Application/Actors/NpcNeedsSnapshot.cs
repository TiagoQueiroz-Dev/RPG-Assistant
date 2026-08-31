namespace RpgWorld.Application.Actors;

public sealed record NpcNeedsSnapshot(
    Guid ActorId,
    Guid WorldId,
    int X,
    int Y,
    decimal Hunger,
    decimal Energy,
    decimal Money,
    string? Job,
    Guid? FactionId);

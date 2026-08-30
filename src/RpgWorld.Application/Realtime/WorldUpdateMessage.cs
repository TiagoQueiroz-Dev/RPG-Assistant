namespace RpgWorld.Application.Realtime;

public sealed record WorldUpdateMessage(
    Guid MessageId,
    Guid WorldId,
    string UpdateType,
    DateTimeOffset OccurredAtUtc,
    IReadOnlyDictionary<string, string?> Data);


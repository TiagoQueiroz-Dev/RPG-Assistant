namespace RpgWorld.Application.Worlds.Editing;

public sealed record MapEditResult(
    Guid OperationId,
    Guid WorldId,
    int AffectedTiles,
    string Status);

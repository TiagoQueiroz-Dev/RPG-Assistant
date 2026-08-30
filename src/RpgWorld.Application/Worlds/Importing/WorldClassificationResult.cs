namespace RpgWorld.Application.Worlds.Importing;

public sealed record WorldClassificationResult(
    Guid WorldId,
    int AutomaticallyClassified,
    int PreservedManual,
    string Status);

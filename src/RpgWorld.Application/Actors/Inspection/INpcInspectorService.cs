namespace RpgWorld.Application.Actors.Inspection;

public interface INpcInspectorService
{
    Task<IReadOnlyList<ActorAtPositionView>> ListAtPositionAsync(
        Guid worldId,
        int x,
        int y,
        CancellationToken cancellationToken = default);

    Task<NpcInspectorView?> GetNpcAsync(
        Guid actorId,
        CancellationToken cancellationToken = default);
}

public sealed record ActorAtPositionView(
    Guid ActorId,
    string Name,
    string Kind,
    string? CurrentAction,
    IReadOnlyList<string> TraitCodes);

public sealed record NpcTraitInspectorView(
    string Code,
    string Name,
    string Description,
    IReadOnlyDictionary<string, decimal> ActionScoreMultipliers,
    bool DefinitionAvailable);

public sealed record NpcInspectorView(
    Guid ActorId,
    Guid WorldId,
    string Name,
    int X,
    int Y,
    int Health,
    int MaximumHealth,
    decimal Hunger,
    decimal Energy,
    decimal Money,
    string? Job,
    string? CurrentAction,
    Guid? FactionId,
    IReadOnlyList<NpcTraitInspectorView> Traits);

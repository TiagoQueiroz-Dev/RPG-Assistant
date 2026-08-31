using RpgWorld.Domain.Actors;
using RpgWorld.Domain.Actors.Traits;
using RpgWorld.Domain.Worlds;

namespace RpgWorld.Application.Actors.Inspection;

public sealed class NpcInspectorService(
    IActorRepository repository,
    ITraitDefinitionCatalog traitCatalog) : INpcInspectorService
{
    public async Task<IReadOnlyList<ActorAtPositionView>> ListAtPositionAsync(
        Guid worldId,
        int x,
        int y,
        CancellationToken cancellationToken = default)
    {
        var actors = await repository.ListAtPositionAsync(
            new Position(worldId, x, y),
            cancellationToken);
        return actors.Select(actor => new ActorAtPositionView(
                actor.Id,
                actor.Name,
                actor.Kind,
                actor.CurrentAction,
                actor is NpcActor npc ? npc.TraitCodes : []))
            .ToArray();
    }

    public async Task<NpcInspectorView?> GetNpcAsync(
        Guid actorId,
        CancellationToken cancellationToken = default)
    {
        if (actorId == Guid.Empty) throw new ArgumentException("Actor identifier is required.", nameof(actorId));
        if (await repository.GetAsync(actorId, cancellationToken) is not NpcActor npc) return null;
        return new NpcInspectorView(
            npc.Id,
            npc.WorldId,
            npc.Name,
            npc.X,
            npc.Y,
            npc.Health,
            npc.MaximumHealth,
            npc.Hunger,
            npc.Energy,
            npc.Money,
            npc.Job,
            npc.CurrentAction,
            npc.FactionId,
            npc.TraitCodes.Select(ToView).ToArray());
    }

    private NpcTraitInspectorView ToView(string code) =>
        traitCatalog.TryResolve(code, out var trait)
            ? new NpcTraitInspectorView(
                trait!.Code,
                trait.Name,
                trait.Description,
                trait.ActionScoreMultipliers,
                true)
            : new NpcTraitInspectorView(
                code,
                code,
                "Definition is unavailable because its RPG module is not loaded.",
                new Dictionary<string, decimal>(),
                false);
}

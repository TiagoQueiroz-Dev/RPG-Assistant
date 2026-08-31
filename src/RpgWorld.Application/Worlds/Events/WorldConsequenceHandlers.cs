using RpgWorld.Application.Events;
using RpgWorld.Domain.Actors;
using RpgWorld.Domain.Events;
using RpgWorld.Domain.Worlds.Events;

namespace RpgWorld.Application.Worlds.Events;

public abstract class IdempotentConsequenceHandler(IWorldConsequenceRepository repository)
{
    protected IWorldConsequenceRepository Repository { get; } = repository;

    protected async Task ApplyAsync(
        Guid worldId,
        WorldConsequenceKind kind,
        Guid targetId,
        decimal magnitude,
        string description,
        Guid sourceEventId,
        DateTimeOffset occurredAtUtc,
        CancellationToken cancellationToken)
    {
        if (await Repository.ExistsAsync(sourceEventId, kind, targetId, cancellationToken)) return;
        Repository.Add(WorldConsequence.Create(
            worldId, kind, targetId, magnitude, description, sourceEventId, occurredAtUtc));
        await Repository.SaveChangesAsync(cancellationToken);
    }
}

public sealed class ActorKilledReputationConsequenceHandler(IWorldConsequenceRepository repository)
    : IdempotentConsequenceHandler(repository), IDomainEventHandler<ActorKilledEvent>
{
    public Task HandleAsync(ActorKilledEvent domainEvent, CancellationToken cancellationToken = default) =>
        domainEvent.KillerId is { } killerId
            ? ApplyAsync(domainEvent.WorldId, WorldConsequenceKind.Reputation, killerId, -50m,
                "A killing damaged the perpetrator's reputation.", domainEvent.EventId,
                domainEvent.OccurredAtUtc, cancellationToken)
            : Task.CompletedTask;
}

public sealed class ActorKilledCrimeConsequenceHandler(IWorldConsequenceRepository repository)
    : IdempotentConsequenceHandler(repository), IDomainEventHandler<ActorKilledEvent>
{
    public Task HandleAsync(ActorKilledEvent domainEvent, CancellationToken cancellationToken = default) =>
        domainEvent.KillerId is { } killerId
            ? ApplyAsync(domainEvent.WorldId, WorldConsequenceKind.Crime, killerId, 100m,
                "A homicide was recorded.", domainEvent.EventId, domainEvent.OccurredAtUtc, cancellationToken)
            : Task.CompletedTask;
}

public sealed class ActorKilledFamilyConsequenceHandler(IWorldConsequenceRepository repository)
    : IdempotentConsequenceHandler(repository), IDomainEventHandler<ActorKilledEvent>
{
    public async Task HandleAsync(ActorKilledEvent domainEvent, CancellationToken cancellationToken = default)
    {
        var count = await Repository.CountLivingFamilyAsync(
            domainEvent.WorldId, domainEvent.ActorId, cancellationToken);
        if (count > 0)
            await ApplyAsync(domainEvent.WorldId, WorldConsequenceKind.Family, domainEvent.ActorId,
                Math.Min(100m, count * 25m), "The victim's family was affected by the death.",
                domainEvent.EventId, domainEvent.OccurredAtUtc, cancellationToken);
    }
}

public sealed class ActorKilledFactionConsequenceHandler(IWorldConsequenceRepository repository)
    : IdempotentConsequenceHandler(repository), IDomainEventHandler<ActorKilledEvent>
{
    public async Task HandleAsync(ActorKilledEvent domainEvent, CancellationToken cancellationToken = default)
    {
        var victim = await Repository.GetActorAsync(domainEvent.ActorId, cancellationToken);
        if (victim?.FactionId is { } factionId)
            await ApplyAsync(domainEvent.WorldId, WorldConsequenceKind.Faction, factionId, -25m,
                "A faction member's death increased internal instability.", domainEvent.EventId,
                domainEvent.OccurredAtUtc, cancellationToken);
    }
}

public sealed class ActorKilledEconomyConsequenceHandler(IWorldConsequenceRepository repository)
    : IdempotentConsequenceHandler(repository), IDomainEventHandler<ActorKilledEvent>
{
    public async Task HandleAsync(ActorKilledEvent domainEvent, CancellationToken cancellationToken = default)
    {
        if (await Repository.GetActorAsync(domainEvent.ActorId, cancellationToken) is NpcActor
            { ResidentCityId: { } cityId, Job: { Length: > 0 } })
            await ApplyAsync(domainEvent.WorldId, WorldConsequenceKind.Economy, cityId, -10m,
                "A working resident's death reduced local economic activity.", domainEvent.EventId,
                domainEvent.OccurredAtUtc, cancellationToken);
    }
}

public sealed class CrimeFactionEscalationHandler(IWorldConsequenceRepository repository)
    : IdempotentConsequenceHandler(repository), IDomainEventHandler<WorldConsequenceAppliedEvent>
{
    public Task HandleAsync(WorldConsequenceAppliedEvent domainEvent, CancellationToken cancellationToken = default) =>
        domainEvent.Kind == WorldConsequenceKind.Crime
            ? ApplyAsync(domainEvent.WorldId, WorldConsequenceKind.Faction, domainEvent.TargetId, -20m,
                "The recorded crime caused political unrest.", domainEvent.EventId,
                domainEvent.OccurredAtUtc, cancellationToken)
            : Task.CompletedTask;
}

public sealed class FactionEconomyEscalationHandler(IWorldConsequenceRepository repository)
    : IdempotentConsequenceHandler(repository), IDomainEventHandler<WorldConsequenceAppliedEvent>
{
    public Task HandleAsync(WorldConsequenceAppliedEvent domainEvent, CancellationToken cancellationToken = default) =>
        domainEvent.Kind == WorldConsequenceKind.Faction
            ? ApplyAsync(domainEvent.WorldId, WorldConsequenceKind.Economy, domainEvent.TargetId, -10m,
                "Political unrest disrupted economic activity.", domainEvent.EventId,
                domainEvent.OccurredAtUtc, cancellationToken)
            : Task.CompletedTask;
}

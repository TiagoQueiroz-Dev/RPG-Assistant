using System.Text.Json;
using RpgWorld.Domain.Events;
using RpgWorld.Domain.Worlds.Events;
using RpgWorld.Domain.Worlds.Resources;

namespace RpgWorld.Infrastructure.Events;

internal static class WorldEventPolicy
{
    private static readonly JsonSerializerOptions PayloadOptions = new(JsonSerializerDefaults.Web);

    public static WorldEvent? Create(IDomainEvent domainEvent)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        var metadata = domainEvent switch
        {
            ActorKilledEvent value => new Metadata(value.WorldId, null, Actors(value.ActorId, value.KillerId)),
            CityCreatedEvent value => new Metadata(value.WorldId, null, []),
            CityCrisisEvent value => new Metadata(value.WorldId, null, []),
            CityDestroyedEvent value => new Metadata(value.WorldId, null, []),
            CityGrowthEvent value => new Metadata(value.WorldId, null, []),
            CityTradeRoutesChangedEvent value => new Metadata(value.WorldId, null, []),
            CitySatisfactionChangedEvent value => new Metadata(value.WorldId, null, []),
            CityResourceShortageEvent value => new Metadata(value.WorldId, null, []),
            CityResourceSurplusEvent value => new Metadata(value.WorldId, null, []),
            FactionCreatedEvent value => new Metadata(value.WorldId, null, [value.LeaderActorId]),
            FactionDiplomaticStateChangedEvent value => new Metadata(value.WorldId, null, []),
            FactionDissolvedEvent value => new Metadata(value.WorldId, null, []),
            FactionLeaderChangedEvent value => new Metadata(
                value.WorldId, null, [value.PreviousLeaderActorId, value.NewLeaderActorId]),
            FactionWarDeclaredEvent value => new Metadata(value.WorldId, null, []),
            NaturalResourceEmergenceEvent value => new Metadata(
                value.WorldId, new WorldEventPosition(value.X, value.Y), []),
            ResourceDiscoveredEvent value => new Metadata(value.WorldId, null, [value.DiscoveredByActorId]),
            ResourceExhaustedEvent value => new Metadata(
                value.WorldId, null,
                value.ConsumerKind == ResourceConsumerKind.Actor ? [value.ConsumerId] : []),
            ResourceSpawnedEvent value when value.SourceWorldEventId is null => new Metadata(value.WorldId, null, []),
            WorldConsequenceAppliedEvent value => new Metadata(value.WorldId, null, []),
            _ => null
        };
        if (metadata is null) return null;
        var type = domainEvent.GetType().Name;
        if (type.EndsWith("Event", StringComparison.Ordinal)) type = type[..^5];
        return WorldEvent.Create(
            domainEvent.EventId,
            metadata.WorldId,
            type,
            domainEvent.OccurredAtUtc,
            metadata.Position,
            metadata.ActorIds,
            JsonSerializer.Serialize(domainEvent, domainEvent.GetType(), PayloadOptions),
            correlationId: domainEvent.CorrelationId,
            causationId: domainEvent.CausationId,
            causalityDepth: domainEvent.CausalityDepth);
    }

    private static Guid[] Actors(Guid actorId, Guid? secondActorId) =>
        secondActorId is { } second ? [actorId, second] : [actorId];

    private sealed record Metadata(Guid WorldId, WorldEventPosition? Position, IReadOnlyList<Guid> ActorIds);
}

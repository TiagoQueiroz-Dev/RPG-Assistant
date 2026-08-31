using System.Globalization;
using RpgWorld.Domain.Actors;
using RpgWorld.Domain.Actors.Memories;
using RpgWorld.Domain.Events;

namespace RpgWorld.Application.Actors.Memories;

public sealed class NpcMemoryEventRecorder(
    IActorRepository actors,
    INpcMemoryRepository memories,
    NpcMemoryOptions options)
{
    public async Task RecordAsync(
        ActorDamagedEvent domainEvent,
        CancellationToken cancellationToken = default)
    {
        if (!options.EnabledEventTypes.Contains(NpcMemoryEventTypes.WasAttacked) ||
            domainEvent.SourceActorId is not { } sourceId ||
            await actors.GetAsync(domainEvent.ActorId, cancellationToken) is not NpcActor npc ||
            npc.Status == ActorStatus.Dead)
            return;
        var importance = Math.Clamp(20 + domainEvent.Damage, 1, 79);
        memories.Add(NpcMemory.Create(
            npc.Id,
            npc.WorldId,
            NpcMemoryEventTypes.WasAttacked,
            sourceId,
            importance,
            domainEvent.OccurredAtUtc,
            options.CalculateExpiration(domainEvent.OccurredAtUtc, importance),
            new Dictionary<string, string>
            {
                ["damage"] = domainEvent.Damage.ToString(CultureInfo.InvariantCulture),
                ["remainingHealth"] = domainEvent.RemainingHealth.ToString(CultureInfo.InvariantCulture)
            }));
        npc.ApplyRelationship(sourceId, new ActorRelationshipModifier(
            NpcMemoryEventTypes.WasAttacked,
            fear: importance,
            respect: -importance / 3,
            hatred: importance,
            trust: -importance), domainEvent.OccurredAtUtc);
        await memories.SaveChangesAsync(cancellationToken);
    }

    public async Task RecordAsync(
        ActorKilledEvent domainEvent,
        CancellationToken cancellationToken = default)
    {
        if (!options.EnabledEventTypes.Contains(NpcMemoryEventTypes.FamilyMemberKilled) ||
            domainEvent.KillerId is not { } killerId)
            return;
        var family = (await actors.ListByWorldAsync(domainEvent.WorldId, cancellationToken))
            .OfType<NpcActor>()
            .Where(npc => npc.Status != ActorStatus.Dead && npc.FamilyIds.Contains(domainEvent.ActorId))
            .ToArray();
        foreach (var relative in family)
        {
            if (await actors.GetAsync(relative.Id, cancellationToken) is not NpcActor tracked) continue;
            memories.Add(NpcMemory.Create(
                tracked.Id,
                tracked.WorldId,
                NpcMemoryEventTypes.FamilyMemberKilled,
                killerId,
                100,
                domainEvent.OccurredAtUtc,
                payload: new Dictionary<string, string> { ["victimId"] = domainEvent.ActorId.ToString() }));
            tracked.ApplyRelationship(killerId, new ActorRelationshipModifier(
                NpcMemoryEventTypes.FamilyMemberKilled,
                fear: 60,
                respect: -50,
                hatred: 100,
                trust: -100), domainEvent.OccurredAtUtc);
        }
        if (family.Length > 0) await memories.SaveChangesAsync(cancellationToken);
    }
}

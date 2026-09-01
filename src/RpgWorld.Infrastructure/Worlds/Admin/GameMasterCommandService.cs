using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RpgWorld.Application.Realtime;
using RpgWorld.Application.Worlds.Admin;
using RpgWorld.Domain.Actors;
using RpgWorld.Domain.Worlds;
using RpgWorld.Domain.Worlds.Cities;
using RpgWorld.Domain.Worlds.Definitions;
using RpgWorld.Domain.Worlds.Events;
using RpgWorld.Domain.Worlds.Factions;
using RpgWorld.Infrastructure.Persistence;

namespace RpgWorld.Infrastructure.Worlds.Admin;

public sealed class GameMasterCommandService(
    RpgWorldDbContext dbContext,
    IWorldDefinitionCatalog definitions,
    IWorldUpdatePublisher publisher,
    TimeProvider timeProvider) : IGameMasterCommandService
{
    public async Task<GameMasterCommandResult> ExecuteAsync(
        Guid worldId,
        GameMasterCommand command,
        CancellationToken cancellationToken = default)
    {
        if (worldId == Guid.Empty) throw new ArgumentException("World identifier is required.", nameof(worldId));
        ArgumentNullException.ThrowIfNull(command);
        if (!Enum.IsDefined(command.Type)) throw new ArgumentOutOfRangeException(nameof(command));
        var world = await dbContext.Worlds.SingleOrDefaultAsync(value => value.Id == worldId, cancellationToken)
            ?? throw new KeyNotFoundException($"World '{worldId}' was not found.");
        if (command.X.HasValue != command.Y.HasValue)
            throw new ArgumentException("A position requires both X and Y coordinates.", nameof(command));
        if (command.X is { } commandX && command.Y is { } commandY) world.PositionAt(commandX, commandY);
        var occurredAtUtc = (command.OccurredAtUtc ?? timeProvider.GetUtcNow()).ToUniversalTime();

        var outcome = command.Type switch
        {
            GameMasterCommandType.CreateNpc => await CreateActorAsync(world, command, occurredAtUtc, false, cancellationToken),
            GameMasterCommandType.CreateCreature => await CreateActorAsync(world, command, occurredAtUtc, true, cancellationToken),
            GameMasterCommandType.DeleteNpc => await DeleteNpcAsync(world, command, occurredAtUtc, cancellationToken),
            GameMasterCommandType.MoveActor => await MoveActorAsync(world, command, occurredAtUtc, cancellationToken),
            GameMasterCommandType.CreateCity => await CreateCityAsync(world, command, occurredAtUtc, cancellationToken),
            GameMasterCommandType.DestroyCity => await DestroyCityAsync(world, command, occurredAtUtc, cancellationToken),
            GameMasterCommandType.AdjustResource => await AdjustResourceAsync(world, command, occurredAtUtc, cancellationToken),
            GameMasterCommandType.ChangeClimate => await ChangeClimateAsync(world, command, cancellationToken),
            GameMasterCommandType.CreateEvent => await CreateEventAsync(world, command, occurredAtUtc, cancellationToken),
            GameMasterCommandType.DeclareWar => await ChangeWarAsync(world, command, occurredAtUtc, true, cancellationToken),
            GameMasterCommandType.EndWar => await ChangeWarAsync(world, command, occurredAtUtc, false, cancellationToken),
            GameMasterCommandType.ChangeFactionRelation => await ChangeRelationAsync(world, command, occurredAtUtc, cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(command))
        };

        var commandId = Guid.CreateVersion7();
        var auditPayload = JsonSerializer.Serialize(new
        {
            schema = "rpg-world.game-master-command",
            version = 1,
            command = command.Type.ToString(),
            command.Reason,
            entityId = outcome.EntityId,
            command.ActorId,
            command.CityId,
            command.ResourceDepositId,
            command.FactionId,
            command.TargetFactionId,
            outcome.Summary
        });
        dbContext.WorldEvents.Add(WorldEvent.Create(
            commandId,
            world.Id,
            $"game-master.{Kebab(command.Type.ToString())}",
            occurredAtUtc,
            EventPosition(command),
            AuditActorIds(command, outcome),
            auditPayload));
        await dbContext.SaveChangesAsync(cancellationToken);

        var result = new GameMasterCommandResult(
            commandId, world.Id, command.Type.ToString(), outcome.EntityId, occurredAtUtc, outcome.Summary);
        var message = new WorldUpdateMessage(
            Guid.CreateVersion7(), world.Id, "game-master-command", occurredAtUtc,
            new Dictionary<string, string?>
            {
                ["commandId"] = commandId.ToString(),
                ["command"] = result.Command,
                ["entityId"] = result.EntityId?.ToString(),
                ["summary"] = result.Summary
            });
        await publisher.PublishToWorldAsync(message, cancellationToken);
        await publisher.PublishToGameMasterAsync(message, cancellationToken);
        if (outcome.PlayerActorId is { } playerActorId)
            await publisher.PublishToPlayerAsync(playerActorId, message, cancellationToken);
        return result;
    }

    private async Task<Outcome> CreateActorAsync(
        World world, GameMasterCommand command, DateTimeOffset instant, bool creature, CancellationToken token)
    {
        var tile = await RequiredTileAsync(world, Required(command.X, "X coordinate"), Required(command.Y, "Y coordinate"), token);
        var position = world.PositionAt(tile.X, tile.Y);
        var maximumHealth = command.MaximumHealth ?? 100;
        Actor actor = creature
            ? CreatureActor.Create(RequiredText(command.Name, "Actor name", 200), world, position, instant, maximumHealth)
            : NpcActor.Create(RequiredText(command.Name, "Actor name", 200), world, position, instant, maximumHealth);
        tile.AddOccupant(actor.Id);
        dbContext.Actors.Add(actor);
        return new Outcome(actor.Id, $"{(creature ? "Creature" : "NPC")} '{actor.Name}' created at {actor.X},{actor.Y}.");
    }

    private async Task<Outcome> DeleteNpcAsync(
        World world, GameMasterCommand command, DateTimeOffset instant, CancellationToken token)
    {
        var actorId = Required(command.ActorId, "NPC identifier");
        var npc = await dbContext.Actors.OfType<NpcActor>().SingleOrDefaultAsync(value => value.Id == actorId, token)
            ?? throw new KeyNotFoundException($"NPC '{actorId}' was not found.");
        EnsureWorld(world, npc.WorldId, "NPC");
        if (npc.Status == ActorStatus.Dead) throw new InvalidOperationException("NPC is already removed from the active world.");
        npc.TakeDamage(npc.Health, null, instant);
        var tile = await RequiredTileAsync(world, npc.X, npc.Y, token);
        tile.RemoveOccupant(npc.Id);
        return new Outcome(npc.Id, $"NPC '{npc.Name}' removed from the active world.");
    }

    private async Task<Outcome> MoveActorAsync(
        World world, GameMasterCommand command, DateTimeOffset instant, CancellationToken token)
    {
        var actorId = Required(command.ActorId, "Actor identifier");
        var actor = await dbContext.Actors.SingleOrDefaultAsync(value => value.Id == actorId, token)
            ?? throw new KeyNotFoundException($"Actor '{actorId}' was not found.");
        EnsureWorld(world, actor.WorldId, "Actor");
        var destination = await RequiredTileAsync(world, Required(command.X, "Destination X"), Required(command.Y, "Destination Y"), token);
        var origin = await RequiredTileAsync(world, actor.X, actor.Y, token);
        actor.Move(world, world.PositionAt(destination.X, destination.Y), instant);
        origin.RemoveOccupant(actor.Id);
        destination.AddOccupant(actor.Id);
        return new Outcome(actor.Id, $"Actor '{actor.Name}' moved to {destination.X},{destination.Y}.",
            actor is PlayerActor ? actor.Id : null);
    }

    private async Task<Outcome> CreateCityAsync(
        World world, GameMasterCommand command, DateTimeOffset instant, CancellationToken token)
    {
        var centerX = Required(command.X, "City center X");
        var centerY = Required(command.Y, "City center Y");
        var requested = command.Territory is { Count: > 0 }
            ? command.Territory
            : [new GameMasterCommandPosition(centerX, centerY)];
        var positions = requested.Select(value => world.PositionAt(value.X, value.Y)).Distinct().ToArray();
        var xValues = positions.Select(value => value.X).Distinct().ToArray();
        var yValues = positions.Select(value => value.Y).Distinct().ToArray();
        var expected = positions.Select(value => (value.X, value.Y)).ToHashSet();
        var persisted = await dbContext.Tiles.Where(tile => tile.WorldId == world.Id &&
            xValues.Contains(tile.X) && yValues.Contains(tile.Y)).Select(tile => new { tile.X, tile.Y }).ToArrayAsync(token);
        if (persisted.Count(value => expected.Contains((value.X, value.Y))) != positions.Length)
            throw new InvalidOperationException("Every city territory position must have a persisted map tile.");
        var occupied = await dbContext.CityTerritoryTiles.Where(tile => tile.WorldId == world.Id && tile.IsActive &&
            xValues.Contains(tile.X) && yValues.Contains(tile.Y)).Select(tile => new { tile.X, tile.Y }).ToArrayAsync(token);
        var overlaps = occupied.Any(value => expected.Contains((value.X, value.Y)));
        if (overlaps) throw new InvalidOperationException("City territory overlaps an existing city.");
        if (command.FactionId is { } factionId)
        {
            var faction = await dbContext.Factions.AsNoTracking().SingleOrDefaultAsync(value => value.Id == factionId, token)
                ?? throw new KeyNotFoundException($"Faction '{factionId}' was not found.");
            EnsureWorld(world, faction.WorldId, "Faction");
            if (faction.Status == FactionStatus.Dissolved) throw new InvalidOperationException("A dissolved faction cannot govern a city.");
        }
        var city = City.Create(world, RequiredText(command.Name, "City name", 200), world.PositionAt(centerX, centerY),
            positions, command.InitialPopulation ?? 0, command.InitialWealth ?? 0m, instant, command.FactionId);
        dbContext.Cities.Add(city);
        return new Outcome(city.Id, $"City '{city.Name}' created.");
    }

    private async Task<Outcome> DestroyCityAsync(
        World world, GameMasterCommand command, DateTimeOffset instant, CancellationToken token)
    {
        var cityId = Required(command.CityId, "City identifier");
        var city = await dbContext.Cities.SingleOrDefaultAsync(value => value.Id == cityId, token)
            ?? throw new KeyNotFoundException($"City '{cityId}' was not found.");
        EnsureWorld(world, city.WorldId, "City");
        var residents = await dbContext.Actors.OfType<NpcActor>().Where(value => value.ResidentCityId == city.Id).ToArrayAsync(token);
        city.Destroy(RequiredText(command.Reason, "Destruction reason", 500), instant);
        foreach (var resident in residents) resident.LeaveCity(city.Id, instant);
        return new Outcome(city.Id, $"City '{city.Name}' destroyed.");
    }

    private async Task<Outcome> AdjustResourceAsync(
        World world, GameMasterCommand command, DateTimeOffset instant, CancellationToken token)
    {
        var depositId = Required(command.ResourceDepositId, "Resource deposit identifier");
        var deposit = await dbContext.ResourceDeposits.SingleOrDefaultAsync(value => value.Id == depositId, token)
            ?? throw new KeyNotFoundException($"Resource deposit '{depositId}' was not found.");
        EnsureWorld(world, deposit.WorldId, "Resource deposit");
        var delta = command.ResourceQuantityDelta
            ?? throw new ArgumentException("Resource quantity delta is required.", nameof(command));
        deposit.AdjustQuantity(delta, instant);
        return new Outcome(deposit.Id, $"Resource '{deposit.ResourceCode}' adjusted by {delta:0.##}; quantity is {deposit.Quantity:0.##}.");
    }

    private async Task<Outcome> ChangeClimateAsync(
        World world, GameMasterCommand command, CancellationToken token)
    {
        var tile = await RequiredTileAsync(world, Required(command.X, "X coordinate"), Required(command.Y, "Y coordinate"), token);
        var temperature = command.TemperatureCelsius
            ?? throw new ArgumentException("Temperature is required.", nameof(command));
        var humidity = command.Humidity
            ?? throw new ArgumentException("Humidity is required.", nameof(command));
        tile.SetEnvironment(tile.BiomeCode, definitions, tile.Elevation, temperature, humidity);
        return new Outcome(tile.Id, $"Climate at {tile.X},{tile.Y} changed to {temperature:0.#} C and {humidity:P0} humidity.");
    }

    private async Task<Outcome> CreateEventAsync(
        World world, GameMasterCommand command, DateTimeOffset instant, CancellationToken token)
    {
        if (command.ActorId is { } actorId)
        {
            var actorWorldId = await dbContext.Actors.AsNoTracking().Where(value => value.Id == actorId)
                .Select(value => (Guid?)value.WorldId).SingleOrDefaultAsync(token)
                ?? throw new KeyNotFoundException($"Actor '{actorId}' was not found.");
            EnsureWorld(world, actorWorldId, "Actor");
        }
        var eventId = Guid.CreateVersion7();
        var payload = string.IsNullOrWhiteSpace(command.EventPayload) ? "{}" : command.EventPayload;
        var worldEvent = WorldEvent.Create(eventId, world.Id,
            RequiredText(command.EventType, "Event type", 160), instant, EventPosition(command),
            command.ActorId is { } eventActorId ? [eventActorId] : [], payload);
        dbContext.WorldEvents.Add(worldEvent);
        return new Outcome(eventId, $"World event '{worldEvent.Type}' created.");
    }

    private async Task<Outcome> ChangeWarAsync(
        World world, GameMasterCommand command, DateTimeOffset instant, bool declare, CancellationToken token)
    {
        var (source, target) = await RequiredFactionPairAsync(world, command, token);
        var reason = RequiredText(command.Reason, declare ? "War reason" : "Peace reason", 500);
        if (declare)
        {
            var score = new FactionWarScore(new FactionWarFactors(0m, 0m, 0m, 0m, 0m), 100m, 100m, instant);
            source.DeclareWar(target.Id, score, reason, true, instant);
            target.DeclareWar(source.Id, score, reason, true, instant);
        }
        else
        {
            EndWar(source, target.Id, reason, instant);
            EndWar(target, source.Id, reason, instant);
        }
        return new Outcome(source.Id, $"War between '{source.Name}' and '{target.Name}' {(declare ? "declared" : "ended")}.");
    }

    private async Task<Outcome> ChangeRelationAsync(
        World world, GameMasterCommand command, DateTimeOffset instant, CancellationToken token)
    {
        var (source, target) = await RequiredFactionPairAsync(world, command, token);
        var modifier = new FactionRelationModifier(
            FactionRelationModifierSource.Event,
            RequiredText(command.Reason, "Relation change reason", 500),
            command.AffinityDelta ?? 0,
            command.TensionDelta ?? 0,
            vassalage: command.Vassalage);
        source.ApplyRelationModifier(target.Id, modifier, instant);
        return new Outcome(source.Id, $"Relation from '{source.Name}' to '{target.Name}' changed.");
    }

    private async Task<(Faction Source, Faction Target)> RequiredFactionPairAsync(
        World world, GameMasterCommand command, CancellationToken token)
    {
        var sourceId = Required(command.FactionId, "Faction identifier");
        var targetId = Required(command.TargetFactionId, "Target faction identifier");
        if (sourceId == targetId) throw new InvalidOperationException("A faction cannot target itself.");
        var factions = await dbContext.Factions.Where(value => value.Id == sourceId || value.Id == targetId).ToArrayAsync(token);
        var source = factions.SingleOrDefault(value => value.Id == sourceId)
            ?? throw new KeyNotFoundException($"Faction '{sourceId}' was not found.");
        var target = factions.SingleOrDefault(value => value.Id == targetId)
            ?? throw new KeyNotFoundException($"Faction '{targetId}' was not found.");
        EnsureWorld(world, source.WorldId, "Faction");
        EnsureWorld(world, target.WorldId, "Target faction");
        if (source.Status == FactionStatus.Dissolved || target.Status == FactionStatus.Dissolved)
            throw new InvalidOperationException("Dissolved factions cannot participate in diplomacy.");
        return (source, target);
    }

    private async Task<Tile> RequiredTileAsync(World world, int x, int y, CancellationToken token)
    {
        world.PositionAt(x, y);
        return await dbContext.Tiles.SingleOrDefaultAsync(
            value => value.WorldId == world.Id && value.X == x && value.Y == y, token)
            ?? throw new KeyNotFoundException($"Tile '{x},{y}' was not found.");
    }

    private static void EndWar(Faction faction, Guid targetId, string reason, DateTimeOffset instant)
    {
        var current = faction.Relations.GetValueOrDefault(targetId)
            ?? FactionRelation.Neutral(targetId, faction.CreatedAtUtc);
        if (current.Kind != FactionRelationKind.War)
            throw new InvalidOperationException("The factions are not at war.");
        faction.ApplyRelationModifier(targetId, new FactionRelationModifier(
            FactionRelationModifierSource.Event, reason, -current.Affinity, -current.Tension, vassalage: false), instant);
    }

    private static WorldEventPosition? EventPosition(GameMasterCommand command) =>
        command.X is { } x && command.Y is { } y ? new WorldEventPosition(x, y) : null;

    private static IReadOnlyList<Guid> AuditActorIds(GameMasterCommand command, Outcome outcome)
    {
        if (command.ActorId is { } actorId) return [actorId];
        return command.Type is GameMasterCommandType.CreateNpc or GameMasterCommandType.CreateCreature &&
            outcome.EntityId is { } createdActorId ? [createdActorId] : [];
    }

    private static int Required(int? value, string name) =>
        value ?? throw new ArgumentException($"{name} is required.");

    private static Guid Required(Guid? value, string name) =>
        value is { } id && id != Guid.Empty ? id : throw new ArgumentException($"{name} is required.");

    private static string RequiredText(string? value, string name, int maximumLength)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException($"{name} is required.");
        var normalized = value.Trim();
        if (normalized.Length > maximumLength) throw new ArgumentException($"{name} cannot exceed {maximumLength} characters.");
        return normalized;
    }

    private static void EnsureWorld(World world, Guid entityWorldId, string entityName)
    {
        if (world.Id != entityWorldId) throw new InvalidOperationException($"{entityName} does not belong to this world.");
    }

    private static string Kebab(string value) => string.Concat(value.Select((character, index) =>
        char.IsUpper(character) && index > 0 ? $"-{char.ToLowerInvariant(character)}" : char.ToLowerInvariant(character).ToString()));

    private sealed record Outcome(Guid? EntityId, string Summary, Guid? PlayerActorId = null);
}

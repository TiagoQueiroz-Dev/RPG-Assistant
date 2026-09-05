using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RpgWorld.Domain.Worlds;
using RpgWorld.Domain.Worlds.Definitions;
using RpgWorld.Infrastructure.Persistence;
using RpgWorld.Infrastructure.Persistence.Repositories;
using RpgWorld.Infrastructure.Worlds.Importing;
using RpgWorld.Application.Worlds.Importing;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.PixelFormats;
using RpgWorld.Modules.Default.Worlds;
using RpgWorld.Application.Worlds.Editing;
using RpgWorld.Application.Actors;
using RpgWorld.Domain.Actors;
using RpgWorld.Domain.Actors.Traits;
using RpgWorld.Domain.Actors.Memories;
using RpgWorld.Application.Actors.Memories;
using RpgWorld.Application.Events;
using RpgWorld.Infrastructure.Events;
using Microsoft.Extensions.DependencyInjection;
using RpgWorld.Application.Actors.Movement;
using RpgWorld.Infrastructure.Worlds.Editing;
using Testcontainers.PostgreSql;
using RpgWorld.Application.Actors.Housing;
using RpgWorld.Domain.Actors.Housing;
using RpgWorld.Simulation.Actors.Housing;
using RpgWorld.Simulation.Engine;
using RpgWorld.Simulation.Time;
using RpgWorld.Application.Worlds.Resources;
using RpgWorld.Application.Worlds;
using RpgWorld.Domain.Worlds.Resources;
using RpgWorld.Simulation.Worlds.Resources;
using RpgWorld.Application.Worlds.Cities;
using RpgWorld.Domain.Worlds.Cities;
using RpgWorld.Simulation.Worlds.Economy;
using RpgWorld.Application.Worlds.Factions;
using RpgWorld.Domain.Worlds.Factions;
using RpgWorld.Simulation.Worlds.Factions;
using RpgWorld.Application.Worlds.Events;
using RpgWorld.Domain.Worlds.Events;
using RpgWorld.Domain.Events;
using RpgWorld.Application.Worlds.Admin;
using RpgWorld.Application.Realtime;
using RpgWorld.Infrastructure.Worlds.Admin;
using RpgWorld.Application.Worlds.Visibility;
using RpgWorld.Infrastructure.Worlds.Visibility;
using RpgWorld.Api.WorldMaps;
using RpgWorld.Application.Worlds.Content;
using RpgWorld.Domain.Worlds.Content;
using RpgWorld.Infrastructure.Worlds.Content;
using RpgWorld.Modules.Abstractions;
using RpgWorld.Modules.Default;
using RpgWorld.Infrastructure.Worlds;
using RpgWorld.Infrastructure.Worlds.Seeding;
using RpgWorld.Infrastructure;
using RpgWorld.Simulation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using RpgWorld.Simulation.Actors.Actions;
using RpgWorld.Domain.Actors.Actions;
using Microsoft.Extensions.Logging.Abstractions;

namespace RpgWorld.Infrastructure.Tests.Persistence;

public sealed class RpgWorldDbContextPostgreSqlTests : IAsyncLifetime
{
    private static readonly WorldDefinitionCatalog TestDefinitions = new(
        [new TerrainDefinition("plains", "Plains", 1m, true, false)],
        [new BiomeDefinition("temperate", "Temperate", "plains", -10m, 40m, 0.20m, 0.90m)]);

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("rpg_world_tests")
        .WithUsername("rpg_world_tests")
        .WithPassword(Guid.NewGuid().ToString("N"))
        .Build();

    public Task InitializeAsync() => _postgres.StartAsync();

    public Task DisposeAsync() => _postgres.DisposeAsync().AsTask();

    [Fact]
    public async Task Migration_can_write_and_read_a_checkpoint()
    {
        var options = new DbContextOptionsBuilder<RpgWorldDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .Options;

        await using var context = new RpgWorldDbContext(options);
        await context.Database.MigrateAsync();

        var checkpointId = Guid.NewGuid();
        using var metadata = JsonDocument.Parse("""{"source":"integration-test"}""");

        context.PersistenceCheckpoints.Add(new PersistenceCheckpoint(
            checkpointId,
            DateTimeOffset.UtcNow,
            PersistenceCheckpointStatus.Succeeded,
            metadata));

        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var stored = await context.PersistenceCheckpoints
            .SingleAsync(checkpoint => checkpoint.Id == checkpointId);

        Assert.Equal(PersistenceCheckpointStatus.Succeeded, stored.Status);
        Assert.Equal(
            "integration-test",
            stored.Metadata.RootElement.GetProperty("source").GetString());
    }

    [Fact]
    public async Task Spatial_entities_are_persisted_and_queryable_by_position_and_chunk()
    {
        var options = new DbContextOptionsBuilder<RpgWorldDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .Options;

        await using var context = new RpgWorldDbContext(options);
        await context.Database.MigrateAsync();

        var world = World.Create("Aster", width: 65, height: 33);
        var chunkCoordinate = new ChunkCoordinate(2, 1);
        var chunk = world.CreateChunk(chunkCoordinate);
        var position = world.PositionAt(64, 32);
        var tile = world.CreateTile(
            position,
            "temperate",
            TestDefinitions,
            elevation: 42,
            temperatureCelsius: 18.5m,
            humidity: 0.65m);
        var actorId = Guid.NewGuid();
        tile.AddOccupant(actorId);

        context.AddRange(world, chunk, tile);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var repository = new EfWorldMapRepository(context);
        var storedTile = await repository.GetTileAsync(position);
        var storedChunk = await repository.GetChunkAsync(world.Id, chunkCoordinate);
        var chunkTiles = await repository.GetTilesAsync(world.Id, chunkCoordinate);

        Assert.NotNull(storedTile);
        Assert.Equal(actorId, Assert.Single(storedTile.OccupantIds));
        Assert.Equal(chunk.Id, storedChunk?.Id);
        Assert.Equal(tile.Id, Assert.Single(chunkTiles).Id);
    }

    [Fact]
    public async Task Simulation_repository_returns_only_running_worlds_and_persists_control_state()
    {
        var options = new DbContextOptionsBuilder<RpgWorldDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .Options;
        await using var context = new RpgWorldDbContext(options);
        await context.Database.MigrateAsync();
        var running = World.Create("Running", 8, 8);
        var paused = World.Create("Paused", 8, 8);
        paused.PauseSimulation();
        context.Worlds.AddRange(running, paused);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        var repository = new EfWorldSimulationRepository(context);

        Assert.Equal([running.Id], await repository.ListRunningWorldIdsAsync());

        var loadedPaused = await repository.GetAsync(paused.Id);
        Assert.NotNull(loadedPaused);
        loadedPaused.StartSimulation();
        await repository.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var runningIds = await repository.ListRunningWorldIdsAsync();
        Assert.Equal(2, runningIds.Count);
        Assert.Contains(running.Id, runningIds);
        Assert.Contains(paused.Id, runningIds);
    }

    [Fact]
    public async Task Region_simulation_level_and_aggregate_survive_reload()
    {
        var options = new DbContextOptionsBuilder<RpgWorldDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .Options;
        await using var context = new RpgWorldDbContext(options);
        await context.Database.MigrateAsync();
        var world = World.Create("Aggregated", 32, 32);
        var chunk = world.CreateChunk(new ChunkCoordinate(0, 0));
        var aggregate = new RegionAggregateState(250, 91.25m, 44m, 73.5m);
        chunk.TransitionSimulationLevel(SimulationLevel.Regional, aggregate);
        context.AddRange(world, chunk);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var stored = await context.Chunks.SingleAsync(candidate => candidate.Id == chunk.Id);

        Assert.Equal(SimulationLevel.Regional, stored.SimulationLevel);
        Assert.Equal(aggregate, stored.GetAggregateState());
        Assert.False(stored.AllowsIndividualActions);
    }

    [Fact]
    public async Task Actor_hierarchy_and_flexible_state_are_persisted_and_queryable()
    {
        var options = new DbContextOptionsBuilder<RpgWorldDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .Options;
        await using var context = new RpgWorldDbContext(options);
        await context.Database.MigrateAsync();
        var world = World.Create("Living world", 16, 16);
        var now = new DateTimeOffset(2026, 8, 30, 12, 0, 0, TimeSpan.Zero);
        var player = PlayerActor.Create("Ayla", world, world.PositionAt(2, 3), now);
        var npc = NpcActor.Create("Smith", world, world.PositionAt(2, 3), now);
        var creature = CreatureActor.Create("Wolf", world, world.PositionAt(8, 9), now, 35);
        var factionId = Guid.NewGuid();
        npc.SetAttribute("crafting", 18, now);
        npc.AddInventory("iron-ingot", 4, now);
        npc.JoinFaction(factionId, now);
        npc.SetReputation(factionId, 40, now);
        npc.SetRelationship(player.Id, "customer", 15, now);
        context.AddRange(world, player, npc, creature);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        var repository = new EfActorRepository(context);

        var actors = await repository.ListByWorldAsync(world.Id);
        var colocated = await repository.ListAtPositionAsync(world.PositionAt(2, 3));
        var storedNpc = Assert.IsType<NpcActor>(await repository.GetAsync(npc.Id));

        Assert.Equal(3, actors.Count);
        Assert.Contains(actors, actor => actor is PlayerActor);
        Assert.Contains(actors, actor => actor is CreatureActor);
        Assert.Equal(2, colocated.Count);
        Assert.Equal(18, storedNpc.Attributes["crafting"]);
        Assert.Equal(4, Assert.Single(storedNpc.Inventory).Quantity);
        Assert.Equal(40, storedNpc.Reputation[factionId]);
        Assert.Equal(player.Id, Assert.Single(storedNpc.Relationships).ActorId);
    }

    [Fact]
    public async Task Npc_daily_state_survives_restart_and_supports_urgent_utility_queries()
    {
        var options = new DbContextOptionsBuilder<RpgWorldDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .Options;
        var start = new DateTimeOffset(2026, 8, 31, 6, 0, 0, TimeSpan.Zero);
        var familyMemberId = Guid.NewGuid();
        var factionId = Guid.NewGuid();
        Guid worldId;
        Guid urgentNpcId;

        await using (var writeContext = new RpgWorldDbContext(options))
        {
            await writeContext.Database.MigrateAsync();
            var world = World.Create("Persistent NPCs", 16, 16);
            var urgentNpc = NpcActor.Create("Hungry smith", world, world.PositionAt(2, 3), start);
            var satisfiedNpc = NpcActor.Create("Rested farmer", world, world.PositionAt(4, 5), start);
            var player = PlayerActor.Create("Player", world, world.PositionAt(2, 3), start);
            urgentNpc.AdvanceNeedsTo(start.AddHours(18));
            urgentNpc.Earn(75m, start.AddHours(18));
            urgentNpc.Spend(15m, start.AddHours(18));
            urgentNpc.AssignJob("blacksmith", start.AddHours(18));
            urgentNpc.SetHome(world, world.PositionAt(6, 7), start.AddHours(18));
            urgentNpc.AddFamilyMember(familyMemberId, start.AddHours(18));
            urgentNpc.JoinFaction(factionId, start.AddHours(18));
            urgentNpc.SetGoal("feed-family", 90, familyMemberId, start.AddHours(18));
            urgentNpc.AddTrait(new TraitDefinition(
                "ambitious",
                "Ambitious",
                "Pursues greater status.",
                new Dictionary<string, decimal> { ["Work"] = 1.3m }), start.AddHours(18));
            writeContext.AddRange(world, urgentNpc, satisfiedNpc, player);
            await writeContext.SaveChangesAsync();
            worldId = world.Id;
            urgentNpcId = urgentNpc.Id;
        }

        await using var readContext = new RpgWorldDbContext(options);
        var repository = new EfNpcNeedsRepository(readContext);
        var stored = await readContext.Actors.OfType<NpcActor>()
            .SingleAsync(npc => npc.Id == urgentNpcId);
        var urgent = await repository.ListUrgentAsync(worldId, minimumHunger: 50m, maximumEnergy: 50m);

        Assert.Equal(72m, stored.Hunger);
        Assert.Equal(46m, stored.Energy);
        Assert.Equal(60m, stored.Money);
        Assert.Equal("blacksmith", stored.Job);
        Assert.Equal(new Position(worldId, 6, 7), stored.Home);
        Assert.Equal(familyMemberId, Assert.Single(stored.FamilyIds));
        Assert.Equal(factionId, stored.FactionId);
        Assert.Equal("feed-family", Assert.Single(stored.Goals).Code);
        Assert.Equal(["ambitious"], stored.TraitCodes);
        Assert.Equal(urgentNpcId, Assert.Single(urgent).ActorId);
    }

    [Fact]
    public async Task Npc_memories_survive_restart_filter_by_target_and_forget_expired_entries()
    {
        var options = new DbContextOptionsBuilder<RpgWorldDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .Options;
        var now = new DateTimeOffset(2026, 8, 31, 6, 0, 0, TimeSpan.Zero);
        var targetId = Guid.NewGuid();
        Guid npcId;

        await using (var writeContext = new RpgWorldDbContext(options))
        {
            await writeContext.Database.MigrateAsync();
            var world = World.Create("Persistent memories", 8, 8);
            var npc = NpcActor.Create("Rememberer", world, world.PositionAt(1, 1), now);
            var relevant = NpcMemory.Create(
                npc.Id, world.Id, NpcMemoryEventTypes.FamilyMemberKilled, targetId, 100, now,
                payload: new Dictionary<string, string> { ["victimId"] = Guid.NewGuid().ToString() });
            var expired = NpcMemory.Create(
                npc.Id, world.Id, NpcMemoryEventTypes.WasAttacked, targetId, 40, now, now.AddHours(1));
            var otherTarget = NpcMemory.Create(
                npc.Id, world.Id, NpcMemoryEventTypes.Helped, Guid.NewGuid(), 60, now);
            writeContext.AddRange(world, npc, relevant, expired, otherTarget);
            await writeContext.SaveChangesAsync();
            npcId = npc.Id;
        }

        await using var readContext = new RpgWorldDbContext(options);
        var repository = new EfNpcMemoryRepository(readContext);
        var byTarget = await repository.ListAsync(npcId, targetId, now.AddHours(2));

        Assert.Equal(NpcMemoryEventTypes.FamilyMemberKilled, Assert.Single(byTarget).EventType);
        Assert.Equal(3, await readContext.NpcMemories.CountAsync(memory => memory.ActorId == npcId));
        Assert.Equal(1, await repository.DeleteExpiredAsync(now.AddHours(2)));
        Assert.Equal(2, await readContext.NpcMemories.CountAsync(memory => memory.ActorId == npcId));
    }

    [Fact]
    public async Task Configured_damage_event_persists_memory_without_recursive_dispatch()
    {
        var services = new ServiceCollection();
        services.AddDbContext<RpgWorldDbContext>(options => options.UseNpgsql(_postgres.GetConnectionString()));
        services.AddScoped<IDomainEventDispatcher, DomainEventDispatcher>();
        services.AddScoped<IActorRepository, EfActorRepository>();
        services.AddScoped<INpcMemoryRepository, EfNpcMemoryRepository>();
        services.AddSingleton(new NpcMemoryOptions());
        services.AddScoped<NpcMemoryEventRecorder>();
        services.AddScoped<IDomainEventHandler<RpgWorld.Domain.Events.ActorDamagedEvent>, NpcDamagedMemoryHandler>();
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<RpgWorldDbContext>();
        await context.Database.MigrateAsync();
        var now = DateTimeOffset.UnixEpoch;
        var world = World.Create("Event memory", 8, 8);
        var npc = NpcActor.Create("Victim", world, world.PositionAt(1, 1), now);
        var attacker = PlayerActor.Create("Attacker", world, world.PositionAt(1, 1), now);
        context.AddRange(world, npc, attacker);
        await context.SaveChangesAsync();

        npc.TakeDamage(25, attacker.Id, now.AddHours(1));
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var memory = await context.NpcMemories.SingleAsync(candidate => candidate.ActorId == npc.Id);
        var storedNpc = Assert.IsType<NpcActor>(await context.Actors.SingleAsync(actor => actor.Id == npc.Id));
        Assert.Equal(NpcMemoryEventTypes.WasAttacked, memory.EventType);
        var relationship = storedNpc.Relationships.Single(candidate => candidate.ActorId == attacker.Id);
        Assert.Equal(45, relationship.Fear);
        Assert.Equal(45, relationship.Hatred);
        Assert.Equal(NpcMemoryEventTypes.WasAttacked, Assert.Single(relationship.History).Reason);
    }

    [Fact]
    public async Task Actor_and_tile_occupancy_move_together_across_chunks()
    {
        var options = new DbContextOptionsBuilder<RpgWorldDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .Options;
        await using var context = new RpgWorldDbContext(options);
        await context.Database.MigrateAsync();
        var world = World.Create("Crossing", 64, 32);
        var originChunk = world.CreateChunk(new ChunkCoordinate(0, 0));
        var destinationChunk = world.CreateChunk(new ChunkCoordinate(1, 0));
        var origin = world.CreateTile(
            world.PositionAt(31, 0),
            "temperate",
            TestDefinitions,
            0,
            20m,
            0.5m);
        var destination = world.CreateTile(
            world.PositionAt(32, 0),
            "temperate",
            TestDefinitions,
            0,
            20m,
            0.5m);
        var actor = PlayerActor.Create("Traveler", world, origin.Position, DateTimeOffset.UnixEpoch);
        origin.AddOccupant(actor.Id);
        context.AddRange(world, originChunk, destinationChunk, origin, destination, actor);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        var store = new EfActorMovementStore(context);
        var storedActor = await store.GetActorAsync(actor.Id);
        var storedWorld = await store.GetWorldAsync(world.Id);
        var storedOrigin = await store.GetTileAsync(origin.Position);
        var storedDestination = await store.GetTileAsync(destination.Position);
        Assert.NotNull(storedActor);
        Assert.NotNull(storedWorld);
        Assert.NotNull(storedOrigin);
        Assert.NotNull(storedDestination);

        storedActor.Move(storedWorld, destination.Position, DateTimeOffset.UnixEpoch.AddMinutes(1));
        storedOrigin.RemoveOccupant(actor.Id);
        storedDestination.AddOccupant(actor.Id);
        await store.SaveChangesAsync();
        context.ChangeTracker.Clear();

        Assert.Equal(destination.Position, (await context.Actors.SingleAsync(candidate => candidate.Id == actor.Id)).Position);
        Assert.DoesNotContain(actor.Id, (await context.Tiles.SingleAsync(tile => tile.Id == origin.Id)).OccupantIds);
        Assert.Contains(actor.Id, (await context.Tiles.SingleAsync(tile => tile.Id == destination.Id)).OccupantIds);
    }

    [Fact]
    public async Task Chunk_changes_are_persisted_and_released_before_reloading()
    {
        var options = new DbContextOptionsBuilder<RpgWorldDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .Options;

        await using var context = new RpgWorldDbContext(options);
        await context.Database.MigrateAsync();

        var world = World.Create("Aster", width: 32, height: 32);
        var coordinate = new ChunkCoordinate(0, 0);
        var chunk = world.CreateChunk(coordinate);
        var tile = world.CreateTile(
            world.PositionAt(0, 0),
            "temperate",
            TestDefinitions,
            elevation: 0,
            temperatureCelsius: 20m,
            humidity: 0.50m);
        context.AddRange(world, chunk, tile);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var repository = new EfWorldMapRepository(context);
        var loadedChunk = await repository.GetChunkAsync(world.Id, coordinate);
        var loadedTiles = await repository.GetTilesAsync(world.Id, coordinate);
        var structureId = Guid.NewGuid();
        Assert.NotNull(loadedChunk);
        Assert.Empty(context.ChangeTracker.Entries<Chunk>());
        Assert.Empty(context.ChangeTracker.Entries<Tile>());
        Assert.Single(loadedTiles).AssignStructure(structureId);

        await repository.PersistAndReleaseChunkAsync(loadedChunk, loadedTiles);

        Assert.Empty(context.ChangeTracker.Entries<Chunk>());
        Assert.Empty(context.ChangeTracker.Entries<Tile>());
        var reloaded = await repository.GetTileAsync(world.PositionAt(0, 0));
        Assert.Equal(structureId, reloaded?.StructureId);
    }

    [Fact]
    public async Task Autonomous_house_reservation_completion_and_family_home_survive_restart()
    {
        var options = new DbContextOptionsBuilder<RpgWorldDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .Options;
        var now = new DateTimeOffset(2032, 4, 5, 8, 0, 0, TimeSpan.Zero);
        var world = World.Create("Persistent housing", 8, 8);
        var builder = NpcActor.Create("Builder", world, world.PositionAt(1, 1), now);
        var family = NpcActor.Create("Family", world, world.PositionAt(1, 1), now);
        builder.AddFamilyMember(family.Id, now);
        builder.AddInventory("wood", 4, now);
        builder.AddInventory("stone", 2, now);
        var tile = world.CreateTile(
            world.PositionAt(2, 1),
            "temperate",
            TestDefinitions,
            elevation: 0,
            temperatureCelsius: 20m,
            humidity: 0.5m);

        await using (var writeContext = new RpgWorldDbContext(options))
        {
            await writeContext.Database.MigrateAsync();
            writeContext.AddRange(world, builder, family, tile);
            await writeContext.SaveChangesAsync();
            var system = new NpcHousingSimulationSystem(
                new EfNpcHousingRepository(writeContext),
                new NpcHousingOptions());
            var firstInstant = now.AddHours(1);
            var context = new SimulationTickContext(
                world.Id,
                new WorldClockSnapshot(world.Id, firstInstant, TimeSpan.FromMinutes(1), 1m, firstInstant));

            await system.ExecuteAsync(context);
            await system.ExecuteAsync(context with
            {
                Clock = context.Clock with { CurrentInstant = firstInstant.AddHours(1) }
            });
        }

        await using var readContext = new RpgWorldDbContext(options);
        var storedConstruction = await readContext.HousingConstructions.AsNoTracking()
            .SingleAsync(construction => construction.OwnerActorId == builder.Id);
        var storedTile = await readContext.Tiles.AsNoTracking().SingleAsync(candidate => candidate.Id == tile.Id);
        var residents = await readContext.Actors.OfType<NpcActor>().AsNoTracking()
            .Where(actor => actor.Id == builder.Id || actor.Id == family.Id)
            .OrderBy(actor => actor.Id)
            .ToArrayAsync();

        Assert.Equal(2, residents.Length);
        Assert.Equal(HousingConstructionStatus.Completed, storedConstruction.Status);
        Assert.Equal(100, storedConstruction.Progress);
        Assert.Equal(storedConstruction.Id, storedTile.StructureId);
        Assert.Contains(builder.Id, storedConstruction.ResidentActorIds);
        Assert.Contains(family.Id, storedConstruction.ResidentActorIds);
        Assert.All(residents, resident =>
        {
            Assert.Equal(storedConstruction.Position, resident.Home);
            Assert.Equal(storedConstruction.Id, resident.HomeStructureId);
            Assert.DoesNotContain(resident.Goals, goal => goal.Code == NpcGoalCodes.NeedHouse);
        });
        var storedBuilder = Assert.Single(residents, resident => resident.Id == builder.Id);
        Assert.Equal(0, storedBuilder.InventoryQuantity("wood"));
        Assert.Equal(0, storedBuilder.InventoryQuantity("stone"));
    }

    [Fact]
    public async Task Natural_resource_discovery_exhaustion_regeneration_and_consumption_survive_restart()
    {
        var options = new DbContextOptionsBuilder<RpgWorldDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .Options;
        var now = new DateTimeOffset(2033, 6, 7, 9, 0, 0, TimeSpan.Zero);
        var sourceWorldEventId = Guid.NewGuid();
        var world = World.Create("Persistent resources", 8, 8);
        var tile = world.CreateTile(
            world.PositionAt(2, 2),
            "forest",
            DefaultWorldDefinitions.Catalog,
            0,
            20m,
            0.5m);
        var actor = NpcActor.Create("Gatherer", world, tile.Position, now);
        var deposit = ResourceDeposit.SpawnOnTile(
            world,
            tile,
            DefaultWorldDefinitions.Catalog.ResolveResource("wood"),
            now,
            initialQuantity: 5m,
            capacity: 10m,
            regenerationPerWorldHour: 2m,
            sourceWorldEventId: sourceWorldEventId);
        deposit.Discover(actor.Id, now);
        deposit.Extract(5m, ResourceConsumer.Actor(actor.Id), now);

        await using (var writeContext = new RpgWorldDbContext(options))
        {
            await writeContext.Database.MigrateAsync();
            writeContext.AddRange(world, tile, actor, deposit);
            await writeContext.SaveChangesAsync();
        }

        await using (var regenerationContext = new RpgWorldDbContext(options))
        {
            var system = new NaturalResourceRegenerationSystem(
                new EfNaturalResourceRepository(regenerationContext));
            var instant = now.AddHours(2);
            await system.ExecuteAsync(new SimulationTickContext(
                world.Id,
                new WorldClockSnapshot(world.Id, instant, TimeSpan.FromHours(1), 1m, instant)));
            var service = new NaturalResourceService(
                new EfNaturalResourceRepository(regenerationContext),
                DefaultWorldDefinitions.Catalog);
            await service.ConsumeAsync(
                deposit.Id,
                ResourceConsumer.City(Guid.NewGuid()),
                3m,
                instant);
        }

        await using var readContext = new RpgWorldDbContext(options);
        var stored = await readContext.ResourceDeposits.AsNoTracking()
            .SingleAsync(candidate => candidate.Id == deposit.Id);
        var storedTile = await readContext.Tiles.AsNoTracking()
            .SingleAsync(candidate => candidate.Id == tile.Id);

        Assert.True(stored.IsDiscovered);
        Assert.False(stored.IsExhausted);
        Assert.Equal(1m, stored.Quantity);
        Assert.Equal(ResourceConsumerKind.City, stored.LastConsumerKind);
        Assert.Equal(sourceWorldEventId, stored.SourceWorldEventId);
        Assert.Equal(stored.Id, storedTile.ResourceDepositId);
    }

    [Fact]
    public async Task Concurrent_resource_extractions_are_rejected_instead_of_losing_quantity()
    {
        var options = new DbContextOptionsBuilder<RpgWorldDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .Options;
        var now = DateTimeOffset.UnixEpoch;
        var world = World.Create("Concurrent resources", 8, 8);
        var deposit = ResourceDeposit.SpawnInRegion(
            world,
            new ChunkCoordinate(0, 0),
            DefaultWorldDefinitions.Catalog.ResolveResource("stone"),
            now,
            initialQuantity: 5m,
            regenerationPerWorldHour: 0m);
        deposit.Discover(Guid.NewGuid(), now);
        await using (var seedContext = new RpgWorldDbContext(options))
        {
            await seedContext.Database.MigrateAsync();
            seedContext.AddRange(world, deposit);
            await seedContext.SaveChangesAsync();
        }

        await using var firstContext = new RpgWorldDbContext(options);
        await using var secondContext = new RpgWorldDbContext(options);
        var first = await firstContext.ResourceDeposits.SingleAsync(candidate => candidate.Id == deposit.Id);
        var second = await secondContext.ResourceDeposits.SingleAsync(candidate => candidate.Id == deposit.Id);
        first.Extract(1m, ResourceConsumer.City(Guid.NewGuid()), now);
        second.Extract(1m, ResourceConsumer.Construction(Guid.NewGuid()), now);

        await firstContext.SaveChangesAsync();
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => secondContext.SaveChangesAsync());

        await using var readContext = new RpgWorldDbContext(options);
        Assert.Equal(4m, (await readContext.ResourceDeposits.AsNoTracking()
            .SingleAsync(candidate => candidate.Id == deposit.Id)).Quantity);
    }

    [Fact]
    public async Task City_population_territory_residents_and_destruction_history_survive_restart()
    {
        var options = new DbContextOptionsBuilder<RpgWorldDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .Options;
        var now = new DateTimeOffset(2034, 7, 8, 10, 0, 0, TimeSpan.Zero);
        var world = World.Create("Persistent city", 8, 8);
        var positions = new[] { world.PositionAt(1, 1), world.PositionAt(2, 1), world.PositionAt(2, 2) };
        var tiles = positions.Select(position => world.CreateTile(
            position, "temperate", TestDefinitions, 0, 20m, 0.5m)).ToArray();
        var npc = NpcActor.Create("Citizen", world, positions[0], now);
        await using (var seedContext = new RpgWorldDbContext(options))
        {
            await seedContext.Database.MigrateAsync();
            seedContext.AddRange(world, npc);
            seedContext.Tiles.AddRange(tiles);
            await seedContext.SaveChangesAsync();
        }

        Guid cityId;
        await using (var createContext = new RpgWorldDbContext(options))
        {
            var service = new CityService(new EfCityRepository(createContext));
            var created = await service.CreateAsync(new CreateCityRequest(
                world.Id,
                "Stonebridge",
                1,
                1,
                positions.Select(position => new CityTerritoryPosition(position.X, position.Y)).ToArray(),
                InitialPopulation: 20,
                InitialWealth: 300m,
                FoundedAtUtc: now,
                GoverningFactionId: Guid.NewGuid(),
                ResidentActorIds: [npc.Id]));
            cityId = created.CityId;
        }

        await using (var evolveContext = new RpgWorldDbContext(options))
        {
            var service = new CityService(new EfCityRepository(evolveContext));
            var loaded = await service.GetAsync(cityId);
            Assert.NotNull(loaded);
            Assert.Equal(3, loaded.Territory.Count);
            Assert.Equal(npc.Id, Assert.Single(loaded.ResidentActorIds));
            await service.ChangePopulationAsync(cityId, 5, "New families arrived.", now.AddHours(1));
            await service.BeginCrisisAsync(cityId, "Flood", 70, now.AddHours(2));
            await service.DestroyAsync(cityId, "The river consumed the city.", now.AddHours(3));
        }

        await using (var readContext = new RpgWorldDbContext(options))
        {
            var stored = await readContext.Cities.AsNoTracking().Include("_territoryTiles")
                .SingleAsync(city => city.Id == cityId);
            var storedNpc = Assert.IsType<NpcActor>(await readContext.Actors.AsNoTracking()
                .SingleAsync(actor => actor.Id == npc.Id));
            Assert.Equal(CityStatus.Destroyed, stored.Status);
            Assert.Equal(0, stored.Population);
            Assert.Null(storedNpc.ResidentCityId);
            Assert.Equal(
                [CityHistoryEventTypes.Founded, CityHistoryEventTypes.ResidentAssociated,
                    CityHistoryEventTypes.Growth, CityHistoryEventTypes.Crisis, CityHistoryEventTypes.Destroyed],
                stored.History.Select(entry => entry.EventType));
            Assert.All(stored.TerritoryTiles, territory => Assert.False(territory.IsActive));
        }

        await using (var successorContext = new RpgWorldDbContext(options))
        {
            var service = new CityService(new EfCityRepository(successorContext));
            var successor = await service.CreateAsync(new CreateCityRequest(
                world.Id,
                "New Stonebridge",
                1,
                1,
                positions.Select(position => new CityTerritoryPosition(position.X, position.Y)).ToArray(),
                2,
                10m,
                now.AddDays(1)));
            Assert.NotEqual(cityId, successor.CityId);
        }
    }

    [Fact]
    public async Task City_economy_extracts_territorial_deposit_and_persists_market_snapshot()
    {
        var options = new DbContextOptionsBuilder<RpgWorldDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .Options;
        var now = DateTimeOffset.UnixEpoch;
        var world = World.Create("Persistent economy", 8, 8);
        var position = world.PositionAt(1, 1);
        var tile = world.CreateTile(
            position, "grassland", DefaultWorldDefinitions.Catalog, 0, 20m, 0.5m);
        var city = City.Create(world, "Granary", position, [position], 10, 100m, now);
        var deposit = ResourceDeposit.SpawnOnTile(
            world,
            tile,
            DefaultWorldDefinitions.Catalog.ResolveResource("food"),
            now,
            initialQuantity: 50m);
        deposit.Discover(Guid.NewGuid(), now.AddMinutes(1));
        var cityId = city.Id;
        var depositId = deposit.Id;

        await using (var createContext = new RpgWorldDbContext(options))
        {
            await createContext.Database.MigrateAsync();
            createContext.Worlds.Add(world);
            createContext.Tiles.Add(tile);
            createContext.Cities.Add(city);
            createContext.ResourceDeposits.Add(deposit);
            await createContext.SaveChangesAsync();
        }

        await using (var cycleContext = new RpgWorldDbContext(options))
        {
            var economyOptions = new CityEconomyOptions
            {
                Resources =
                [
                    new CityEconomyResourceOptions
                    {
                        ResourceCode = "food",
                        NaturalResourceCode = "food",
                        NaturalExtractionPerResident = 1m,
                        ConsumptionPerResident = 1m,
                        BasePrice = 2m,
                        TargetStockPerResident = 2m
                    }
                ]
            };
            economyOptions.Validate();
            var system = new CityEconomySimulationSystem(
                new EfCityEconomyRepository(cycleContext), economyOptions);
            var instant = now.AddHours(1);
            await system.ExecuteAsync(new SimulationTickContext(
                world.Id,
                new WorldClockSnapshot(world.Id, instant, TimeSpan.FromHours(1), 1m, instant)));
        }

        await using var readContext = new RpgWorldDbContext(options);
        var storedCity = await readContext.Cities.AsNoTracking().SingleAsync(candidate => candidate.Id == cityId);
        var storedDeposit = await readContext.ResourceDeposits.AsNoTracking()
            .SingleAsync(candidate => candidate.Id == depositId);
        var market = storedCity.ResourceMarkets["food"];
        Assert.Equal(1, storedCity.EconomicCycleCount);
        Assert.Equal(now.AddHours(1), storedCity.LastEconomicCycleAtUtc);
        Assert.Equal(10m, market.Produced);
        Assert.Equal(10m, market.Consumed);
        Assert.Equal(CityMarketCondition.Shortage, market.Condition);
        Assert.Contains(storedCity.History,
            entry => entry.EventType == CityHistoryEventTypes.ResourceShortage);
        Assert.Equal(44m, storedDeposit.Quantity);
        Assert.Equal(ResourceConsumerKind.City, storedDeposit.LastConsumerKind);
        Assert.Equal(cityId, storedDeposit.LastConsumerId);
    }

    [Fact]
    public async Task Faction_lifecycle_persists_members_leadership_city_territory_and_history()
    {
        var options = new DbContextOptionsBuilder<RpgWorldDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .Options;
        var now = DateTimeOffset.UnixEpoch;
        var world = World.Create("Persistent factions", 8, 8);
        var firstPosition = world.PositionAt(1, 1);
        var secondPosition = world.PositionAt(2, 1);
        var firstTile = world.CreateTile(
            firstPosition, "grassland", DefaultWorldDefinitions.Catalog, 0, 20m, 0.5m);
        var secondTile = world.CreateTile(
            secondPosition, "grassland", DefaultWorldDefinitions.Catalog, 0, 20m, 0.5m);
        var founder = NpcActor.Create("Founder", world, firstPosition, now);
        var successor = NpcActor.Create("Successor", world, secondPosition, now);
        var city = City.Create(
            world, "Capital", firstPosition, [firstPosition, secondPosition], 20, 100m, now);
        Guid factionId;

        await using (var createContext = new RpgWorldDbContext(options))
        {
            await createContext.Database.MigrateAsync();
            createContext.AddRange(world, firstTile, secondTile, founder, successor, city);
            await createContext.SaveChangesAsync();
            var service = new FactionService(new EfFactionRepository(createContext));
            var created = await service.CreateAsync(new CreateFactionRequest(
                world.Id,
                "Amber Crown",
                FactionType.Kingdom,
                founder.Id,
                500m,
                80m,
                now.AddHours(1),
                [new(1, 1)]));
            await service.AddMemberAsync(created.FactionId, successor.Id, now.AddHours(2));
            await service.AssociateCityAsync(
                created.FactionId, city.Id, claimCityTerritory: true, now.AddHours(3));
            await service.ChangeLeaderAsync(
                created.FactionId, successor.Id, "Peaceful succession.", now.AddHours(4));
            factionId = created.FactionId;
        }

        await using (var queryContext = new RpgWorldDbContext(options))
        {
            var service = new FactionService(new EfFactionRepository(queryContext));
            var persisted = await service.GetAsync(factionId);
            Assert.NotNull(persisted);
            Assert.Equal(FactionType.Kingdom.ToString(), persisted.Type);
            Assert.Equal(successor.Id, persisted.LeaderActorId);
            Assert.Equal([founder.Id, successor.Id], persisted.MemberActorIds);
            Assert.Equal(city.Id, Assert.Single(persisted.ControlledCityIds));
            Assert.Equal(2, persisted.Territory.Count);
            Assert.Equal(500m, persisted.Wealth);
            Assert.Equal(80m, persisted.MilitaryPower);
            await service.DissolveAsync(factionId, "The kingdom fragmented.", now.AddHours(5));
        }

        await using var readContext = new RpgWorldDbContext(options);
        var storedFaction = await readContext.Factions.AsNoTracking().Include("_territoryTiles")
            .SingleAsync(faction => faction.Id == factionId);
        var storedMembers = await readContext.Actors.AsNoTracking()
            .Where(actor => actor.Id == founder.Id || actor.Id == successor.Id)
            .ToArrayAsync();
        var storedCity = await readContext.Cities.AsNoTracking().SingleAsync(candidate => candidate.Id == city.Id);
        Assert.Equal(FactionStatus.Dissolved, storedFaction.Status);
        Assert.Null(storedFaction.LeaderActorId);
        Assert.All(storedMembers, actor => Assert.Null(actor.FactionId));
        Assert.Null(storedCity.GoverningFactionId);
        Assert.All(storedFaction.TerritoryTiles, tile => Assert.False(tile.IsActive));
        Assert.Equal(FactionHistoryEventTypes.Dissolved, storedFaction.History[^1].EventType);
        Assert.Contains(storedFaction.History, entry => entry.EventType == FactionHistoryEventTypes.LeaderChanged);
    }

    [Fact]
    public async Task Directed_diplomacy_persists_neutral_to_hostile_to_war_with_event_history()
    {
        var options = new DbContextOptionsBuilder<RpgWorldDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .Options;
        var now = DateTimeOffset.UnixEpoch;
        var world = World.Create("Persistent diplomacy", 8, 8);
        var northLeader = NpcActor.Create("North leader", world, world.PositionAt(1, 1), now);
        var southLeader = NpcActor.Create("South leader", world, world.PositionAt(6, 6), now);
        var borderEventId = Guid.NewGuid();
        Guid northId;
        Guid southId;

        await using (var createContext = new RpgWorldDbContext(options))
        {
            await createContext.Database.MigrateAsync();
            createContext.AddRange(world, northLeader, southLeader);
            await createContext.SaveChangesAsync();
            var service = new FactionService(new EfFactionRepository(createContext));
            northId = (await service.CreateAsync(new CreateFactionRequest(
                world.Id, "North", FactionType.Kingdom, northLeader.Id, 0m, 10m, now.AddHours(1)))).FactionId;
            southId = (await service.CreateAsync(new CreateFactionRequest(
                world.Id, "South", FactionType.Kingdom, southLeader.Id, 0m, 10m, now.AddHours(1)))).FactionId;
            var hostile = await service.ApplyRelationModifierAsync(
                northId,
                southId,
                new FactionRelationModifier(
                    FactionRelationModifierSource.Event,
                    "A caravan was attacked.",
                    affinityDelta: -35,
                    tensionDelta: 55,
                    sourceEventId: borderEventId),
                now.AddHours(2));
            Assert.Equal("Hostile", Assert.Single(hostile.Relations).State);
        }

        await using (var warContext = new RpgWorldDbContext(options))
        {
            var service = new FactionService(new EfFactionRepository(warContext));
            var war = await service.ApplyRelationModifierAsync(
                northId,
                southId,
                new FactionRelationModifier(
                    FactionRelationModifierSource.Border,
                    "Armies crossed the border.",
                    tensionDelta: 30),
                now.AddHours(3));
            Assert.Equal("War", Assert.Single(war.Relations).State);
        }

        await using var readContext = new RpgWorldDbContext(options);
        var north = await readContext.Factions.AsNoTracking().SingleAsync(faction => faction.Id == northId);
        var south = await readContext.Factions.AsNoTracking().SingleAsync(faction => faction.Id == southId);
        var relation = north.Relations[southId];
        Assert.Equal(FactionRelationKind.War, relation.Kind);
        Assert.Equal(-35, relation.Affinity);
        Assert.Equal(85, relation.Tension);
        Assert.Equal(2, relation.History.Count);
        Assert.Equal(borderEventId, relation.History[0].SourceEventId);
        Assert.Equal(FactionRelationKind.Neutral, relation.History[0].PreviousState);
        Assert.Equal(FactionRelationKind.Hostile, relation.History[0].State);
        Assert.Equal(FactionRelationKind.War, relation.History[1].State);
        Assert.Empty(south.Relations);
        Assert.Equal(2, north.History.Count(entry =>
            entry.EventType == FactionHistoryEventTypes.DiplomaticStateChanged));
    }

    [Fact]
    public async Task Emergent_war_score_and_declaration_survive_context_restart()
    {
        var options = new DbContextOptionsBuilder<RpgWorldDbContext>()
            .UseNpgsql(_postgres.GetConnectionString()).Options;
        var now = DateTimeOffset.UnixEpoch;
        var world = World.Create("Persistent emergent war", 8, 8);
        var firstLeader = NpcActor.Create("First leader", world, world.PositionAt(1, 1), now);
        var secondLeader = NpcActor.Create("Second leader", world, world.PositionAt(6, 6), now);
        Guid firstId;
        Guid secondId;

        await using (var context = new RpgWorldDbContext(options))
        {
            await context.Database.MigrateAsync();
            context.AddRange(world, firstLeader, secondLeader);
            await context.SaveChangesAsync();
            var service = new FactionService(new EfFactionRepository(context));
            firstId = (await service.CreateAsync(new CreateFactionRequest(
                world.Id, "First", FactionType.Army, firstLeader.Id, 0m, 100m, now.AddHours(1)))).FactionId;
            secondId = (await service.CreateAsync(new CreateFactionRequest(
                world.Id, "Second", FactionType.Army, secondLeader.Id, 0m, 10m, now.AddHours(1)))).FactionId;
        }

        await using (var simulationContext = new RpgWorldDbContext(options))
        {
            var repository = new EfFactionRepository(simulationContext);
            var warOptions = new WarDeclarationOptions
            {
                DeclareWarThreshold = 50m,
                BorderConflictWeight = 0m,
                ResourceDisputeWeight = 0m,
                HistoricalHatredWeight = 0m,
                AggressiveLeaderWeight = 0m,
                WeakEnemyWeight = 100m
            };
            var system = new FactionWarDeclarationSimulationSystem(
                repository, new WarScoreCalculator(warOptions));
            var instant = now.AddHours(2);
            await system.ExecuteAsync(new SimulationTickContext(
                world.Id, new WorldClockSnapshot(world.Id, instant, TimeSpan.FromMinutes(30), 1m, instant)));
        }

        await using var readContext = new RpgWorldDbContext(options);
        var first = await readContext.Factions.AsNoTracking().SingleAsync(faction => faction.Id == firstId);
        var relation = first.Relations[secondId];
        Assert.Equal(FactionRelationKind.War, relation.Kind);
        Assert.Equal(90m, relation.LastWarScore!.Total);
        Assert.Equal(50m, relation.LastWarScore.DeclareWarThreshold);
        Assert.Contains(first.History, entry => entry.EventType == FactionHistoryEventTypes.WarDeclared);
    }

    [Fact]
    public async Task Relevant_domain_events_are_logged_atomically_and_timeline_filters_survive_restart()
    {
        var options = new DbContextOptionsBuilder<RpgWorldDbContext>()
            .UseNpgsql(_postgres.GetConnectionString()).Options;
        var now = new DateTimeOffset(2026, 8, 31, 10, 0, 0, TimeSpan.Zero);
        var world = World.Create("Auditable world", 8, 8);
        var killer = NpcActor.Create("Killer", world, world.PositionAt(1, 1), now);
        var victim = NpcActor.Create("Victim", world, world.PositionAt(2, 1), now);

        await using (var writeContext = new RpgWorldDbContext(options))
        {
            await writeContext.Database.MigrateAsync();
            writeContext.AddRange(world, killer, victim);
            await writeContext.SaveChangesAsync();
            victim.TakeDamage(victim.MaximumHealth, killer.Id, now.AddHours(1));
            await writeContext.SaveChangesAsync();
            writeContext.WorldEvents.Add(WorldEvent.Create(
                Guid.NewGuid(), world.Id, "PositionMarker", now.AddHours(2),
                new WorldEventPosition(4, 5), [], "{\"label\":\"crossroads\"}"));
            writeContext.WorldEvents.AddRange(
                WorldEvent.Create(Guid.NewGuid(), world.Id, "ManualAudit", now.AddHours(3), null, [], "{\"order\":1}"),
                WorldEvent.Create(Guid.NewGuid(), world.Id, "ManualAudit", now.AddHours(4), null, [], "{\"order\":2}"));
            await writeContext.SaveChangesAsync();
        }

        await using var readContext = new RpgWorldDbContext(options);
        var repository = new EfWorldEventRepository(readContext);
        var actorResult = await repository.SearchAsync(new WorldEventQuery(
            world.Id, ActorId: victim.Id, Type: "ActorKilled",
            FromUtc: now.AddMinutes(30), ToUtc: now.AddHours(1)));
        var positionResult = await repository.SearchAsync(new WorldEventQuery(
            world.Id, PositionX: 4, PositionY: 5, SortOrder: WorldEventSortOrder.OldestFirst));
        var secondNewest = await repository.SearchAsync(new WorldEventQuery(
            world.Id, Page: 2, PageSize: 1, Type: "ManualAudit"));

        var killed = Assert.Single(actorResult.Items);
        Assert.Equal(now.AddHours(1), killed.TimestampUtc);
        Assert.Contains(victim.Id, killed.ActorIds);
        Assert.Contains(killer.Id, killed.ActorIds);
        Assert.Contains("\"actorId\"", killed.Payload);
        Assert.DoesNotContain("domainEvents", killed.Payload, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("PositionMarker", Assert.Single(positionResult.Items).Type);
        Assert.Equal(2, secondNewest.TotalCount);
        Assert.Equal(now.AddHours(3), Assert.Single(secondNewest.Items).TimestampUtc);
        Assert.Empty(await readContext.WorldEvents.Where(value => value.Type == "ActorDamaged").ToArrayAsync());
    }

    [Fact]
    public async Task Actor_killing_produces_idempotent_multi_system_consequence_chain_with_causality()
    {
        var services = new ServiceCollection();
        services.AddDbContext<RpgWorldDbContext>(options => options.UseNpgsql(_postgres.GetConnectionString()));
        services.AddScoped<IDomainEventDispatcher, DomainEventDispatcher>();
        services.AddScoped<IWorldConsequenceRepository, EfWorldConsequenceRepository>();
        services.AddScoped<IDomainEventHandler<ActorKilledEvent>, ActorKilledReputationConsequenceHandler>();
        services.AddScoped<IDomainEventHandler<ActorKilledEvent>, ActorKilledCrimeConsequenceHandler>();
        services.AddScoped<IDomainEventHandler<ActorKilledEvent>, ActorKilledFamilyConsequenceHandler>();
        services.AddScoped<IDomainEventHandler<ActorKilledEvent>, ActorKilledFactionConsequenceHandler>();
        services.AddScoped<IDomainEventHandler<ActorKilledEvent>, ActorKilledEconomyConsequenceHandler>();
        services.AddScoped<IDomainEventHandler<WorldConsequenceAppliedEvent>, CrimeFactionEscalationHandler>();
        services.AddScoped<IDomainEventHandler<WorldConsequenceAppliedEvent>, FactionEconomyEscalationHandler>();
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<RpgWorldDbContext>();
        await context.Database.MigrateAsync();
        var now = DateTimeOffset.UnixEpoch;
        var world = World.Create("Consequence chain", 8, 8);
        var killer = NpcActor.Create("Killer", world, world.PositionAt(1, 1), now);
        var victim = NpcActor.Create("Merchant", world, world.PositionAt(2, 1), now);
        var relative = NpcActor.Create("Relative", world, world.PositionAt(2, 2), now);
        var city = City.Create(world, "Market", world.PositionAt(2, 1), [world.PositionAt(2, 1)], 10, 100m, now);
        var factionId = Guid.NewGuid();
        victim.JoinFaction(factionId, now);
        victim.JoinCity(city, now);
        victim.AssignJob("merchant", now);
        relative.AddFamilyMember(victim.Id, now);
        context.AddRange(world, killer, victim, relative, city);
        await context.SaveChangesAsync();

        victim.TakeDamage(victim.MaximumHealth, killer.Id, now.AddHours(1));
        var root = Assert.Single(victim.DomainEvents.OfType<ActorKilledEvent>());
        await context.SaveChangesAsync();
        var originalCount = await context.WorldConsequences.CountAsync();

        await scope.ServiceProvider.GetRequiredService<IDomainEventDispatcher>().DispatchAsync([root]);
        Assert.Equal(originalCount, await context.WorldConsequences.CountAsync());
        Assert.True(originalCount >= 5);
        Assert.Contains(await context.WorldConsequences.ToArrayAsync(), value => value.Kind == WorldConsequenceKind.Reputation);
        Assert.Contains(await context.WorldConsequences.ToArrayAsync(), value => value.Kind == WorldConsequenceKind.Crime);
        Assert.Contains(await context.WorldConsequences.ToArrayAsync(), value => value.Kind == WorldConsequenceKind.Family);
        Assert.Contains(await context.WorldConsequences.ToArrayAsync(), value => value.Kind == WorldConsequenceKind.Faction);
        Assert.Contains(await context.WorldConsequences.ToArrayAsync(), value => value.Kind == WorldConsequenceKind.Economy);
        var causalPage = await new EfWorldEventRepository(context).SearchAsync(new WorldEventQuery(
            world.Id, PageSize: 50, SortOrder: WorldEventSortOrder.OldestFirst, CorrelationId: root.EventId));
        var chain = causalPage.Items.OrderBy(value => value.CausalityDepth).ToArray();
        Assert.Contains(chain, value => value.CausalityDepth == 0 && value.Type == "ActorKilled");
        Assert.Contains(chain, value => value.CausalityDepth == 1 && value.Type == "WorldConsequenceApplied");
        Assert.Contains(chain, value => value.CausalityDepth == 2);
        Assert.Contains(chain, value => value.CausalityDepth == 3);
        Assert.All(chain.Where(value => value.CausalityDepth > 0), value => Assert.NotNull(value.CausationId));
    }

    [Fact]
    public async Task Merchant_death_deterministically_disrupts_trade_stock_price_and_city_satisfaction()
    {
        var services = new ServiceCollection();
        services.AddDbContext<RpgWorldDbContext>(options => options.UseNpgsql(_postgres.GetConnectionString()));
        services.AddScoped<IDomainEventDispatcher, DomainEventDispatcher>();
        services.AddScoped<IWorldConsequenceRepository, EfWorldConsequenceRepository>();
        services.AddScoped<ICityEconomyRepository, EfCityEconomyRepository>();
        services.AddScoped<IDomainEventHandler<ActorKilledEvent>, ActorKilledEconomyConsequenceHandler>();
        await using var provider = services.BuildServiceProvider();

        async Task<MerchantDeathOutcome> RunScenarioAsync(string worldName)
        {
            await using var scope = provider.CreateAsyncScope();
            var context = scope.ServiceProvider.GetRequiredService<RpgWorldDbContext>();
            await context.Database.MigrateAsync();
            var start = DateTimeOffset.UnixEpoch;
            var world = World.Create(worldName, 8, 8);
            var cityPosition = world.PositionAt(2, 2);
            var city = City.Create(world, "Market", cityPosition, [cityPosition], 10, 100m, start);
            var merchant = NpcActor.Create("Food merchant", world, cityPosition, start);
            merchant.JoinCity(city, start);
            merchant.AssignJob("merchant", start);
            var clock = WorldClock.Create(world.Id, start, start, TimeSpan.FromHours(1));
            context.AddRange(world, city, merchant, clock);
            await context.SaveChangesAsync();
            var economy = new CityEconomySimulationSystem(
                scope.ServiceProvider.GetRequiredService<ICityEconomyRepository>(),
                new CityEconomyOptions
                {
                    Resources =
                    [
                        new CityEconomyResourceOptions
                        {
                            ResourceCode = "food", ConsumptionPerResident = 1m,
                            TradeImportPerMerchant = 20m, BasePrice = 2m, TargetStockPerResident = 1m
                        }
                    ]
                });

            clock.AdvanceTicks();
            await economy.ExecuteAsync(new SimulationTickContext(world.Id,
                new WorldClockSnapshot(world.Id, clock.CurrentInstant, clock.TickDuration, 1m, start)));
            var operatingMarket = city.ResourceMarkets["food"];
            var operatingRoutes = city.ActiveTradeRouteCount;
            var operatingStock = city.ResourceStocks["food"];
            var operatingSatisfaction = city.Satisfaction;

            merchant.TakeDamage(merchant.MaximumHealth, null, clock.CurrentInstant.AddMinutes(1));
            await context.SaveChangesAsync();
            clock.AdvanceTicks();
            await economy.ExecuteAsync(new SimulationTickContext(world.Id,
                new WorldClockSnapshot(world.Id, clock.CurrentInstant, clock.TickDuration, 1m, start)));
            var disruptedMarket = city.ResourceMarkets["food"];
            var timeline = (await new EfWorldEventRepository(context).SearchAsync(new WorldEventQuery(
                world.Id, PageSize: 100, SortOrder: WorldEventSortOrder.OldestFirst))).Items.ToArray();
            var eventTypes = timeline.Select(value => value.Type).ToArray();
            var actorKilledIndex = Array.IndexOf(eventTypes, "ActorKilled");
            var routeDisruptedIndex = Array.LastIndexOf(eventTypes, "CityTradeRoutesChanged");
            var shortageIndex = Array.LastIndexOf(eventTypes, "CityResourceShortage");
            var satisfactionIndex = Array.LastIndexOf(eventTypes, "CitySatisfactionChanged");

            Assert.True(actorKilledIndex >= 0 && actorKilledIndex < routeDisruptedIndex);
            Assert.True(routeDisruptedIndex >= 0 && shortageIndex >= 0 && satisfactionIndex >= 0);
            Assert.True(timeline[actorKilledIndex].TimestampUtc < timeline[routeDisruptedIndex].TimestampUtc);
            Assert.Equal(timeline[routeDisruptedIndex].TimestampUtc, timeline[shortageIndex].TimestampUtc);
            Assert.Equal(timeline[routeDisruptedIndex].TimestampUtc, timeline[satisfactionIndex].TimestampUtc);
            Assert.Contains(timeline, value => value.Type == "WorldConsequenceApplied" && value.CausalityDepth == 1);
            return new MerchantDeathOutcome(
                operatingRoutes, city.ActiveTradeRouteCount,
                operatingStock, city.ResourceStocks.GetValueOrDefault("food"),
                operatingMarket.UnitPrice, disruptedMarket.UnitPrice,
                operatingSatisfaction, city.Satisfaction,
                string.Join(',', eventTypes.Order(StringComparer.Ordinal)));
        }

        var first = await RunScenarioAsync("Merchant consequence one");
        var second = await RunScenarioAsync("Merchant consequence two");

        Assert.Equal(first, second);
        Assert.Equal((1, 0), (first.OperatingRoutes, first.FinalRoutes));
        Assert.Equal((10m, 0m), (first.OperatingStock, first.FinalStock));
        Assert.Equal((2m, 8m), (first.OperatingPrice, first.FinalPrice));
        Assert.Equal((76m, 71m), (first.OperatingSatisfaction, first.FinalSatisfaction));
    }

    [Theory]
    [InlineData(49_777)]
    [InlineData(int.MinValue)]
    [InlineData(int.MaxValue)]
    public async Task Reference_scenario_is_idempotent_complete_simulatable_and_profile_aware(int seed)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:RpgWorld"] = _postgres.GetConnectionString(),
            ["Redis:Enabled"] = "false"
        }).Build();
        var time = new FixedTimeProvider(DateTimeOffset.UnixEpoch.AddHours(1));
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddSingleton<TimeProvider>(time);
        var moduleCatalog = new RpgModuleCatalog([new DefaultRpgModule()]);
        var content = moduleCatalog.Load(["rpgworld.default"]);
        services.AddSingleton<IRpgModuleCatalog>(moduleCatalog);
        services.AddSingleton<IRpgContentCatalog>(content);
        services.AddSingleton<IWorldDefinitionCatalog>(content);
        services.AddInfrastructure(configuration);
        services.AddSimulation();
        await using var provider = services.BuildServiceProvider();

        ReferenceScenarioResult first;
        ReferenceScenarioResult second;
        await using (var scope = provider.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<RpgWorldDbContext>();
            await context.Database.MigrateAsync();
            var seeder = scope.ServiceProvider.GetRequiredService<ReferenceScenarioSeeder>();
            first = await seeder.SeedAsync(seed);
            second = await seeder.SeedAsync(seed);
        }

        Assert.True(first.Created);
        Assert.False(second.Created);
        Assert.Equal(first.WorldId, second.WorldId);
        Assert.Equal(first.WorldId, first.GameMasterWorldId);
        Assert.Equal(first.WorldId, first.PlayerWorldId);
        Assert.Equal((64, 32), (first.MapWidth, first.MapHeight));
        Assert.Equal((2, 100, 2, 8),
            (first.CityCount, first.NpcCount, first.FactionCount, first.ResourceDepositCount));
        Assert.True(first.SimulationRunning);

        var engine = provider.GetServices<IHostedService>().OfType<SimulationEngine>().Single();
        await engine.RunCycleAsync();

        await using var readScope = provider.CreateAsyncScope();
        var readContext = readScope.ServiceProvider.GetRequiredService<RpgWorldDbContext>();
        var cities = await readContext.Cities.AsNoTracking()
            .Where(city => city.WorldId == first.WorldId).OrderBy(city => city.Name).ToArrayAsync();
        var npcs = await readContext.Actors.AsNoTracking().OfType<NpcActor>()
            .Where(actor => actor.WorldId == first.WorldId).ToArrayAsync();
        var factions = await readContext.Factions.AsNoTracking()
            .Where(faction => faction.WorldId == first.WorldId).ToArrayAsync();
        var deposits = await readContext.ResourceDeposits.AsNoTracking()
            .Where(deposit => deposit.WorldId == first.WorldId).ToArrayAsync();
        Assert.All(cities, city => Assert.Equal(1, city.EconomicCycleCount));
        Assert.Equal(50, npcs.Count(actor => actor.ResidentCityId == cities[0].Id));
        Assert.Equal(50, npcs.Count(actor => actor.ResidentCityId == cities[1].Id));
        Assert.All(npcs, npc =>
        {
            Assert.NotNull(npc.Job);
            Assert.NotNull(npc.FactionId);
            Assert.NotNull(npc.ResidentCityId);
        });
        Assert.All(factions, faction =>
        {
            Assert.Equal(50, faction.MemberActorIds.Count);
            Assert.Single(faction.Relations);
        });
        Assert.All(deposits, deposit => Assert.True(deposit.IsDiscovered));
        var tiles = await readContext.Tiles.AsNoTracking()
            .Where(tile => tile.WorldId == first.WorldId).ToArrayAsync();
        Assert.Equal(101, tiles.Sum(tile => tile.OccupantIds.Count));
        Assert.All(npcs, npc => Assert.Contains(npc.Id,
            tiles.Single(tile => tile.X == npc.X && tile.Y == npc.Y).OccupantIds));
        Assert.All(tiles.Where(tile => tile.Y == 16), tile =>
            Assert.True(DefaultWorldDefinitions.Catalog.ResolveTerrain(tile.TerrainCode).IsTraversable));

        var gameMasterMap = await new PersistedWorldMapProvider(readContext).GetMapAsync(first.WorldId);
        var playerMap = await new PlayerWorldMapProvider(
            readContext, new PlayerVisibilityService(readContext, time))
            .GetMapAsync(first.WorldId, first.PlayerActorId);
        Assert.NotNull(gameMasterMap);
        Assert.NotNull(playerMap);
        var gameMasterTiles = gameMasterMap.Chunks.SelectMany(chunk => chunk.Tiles).ToArray();
        var playerTiles = playerMap.Chunks.SelectMany(chunk => chunk.Tiles).ToArray();
        Assert.Equal(64 * 32, gameMasterTiles.Length);
        Assert.Equal(7 * 7, playerTiles.Length);
        Assert.Equal(2, gameMasterTiles.Where(tile => tile.CityId.HasValue)
            .Select(tile => tile.CityId).Distinct().Count());
        Assert.Single(playerTiles.Where(tile => tile.CityId.HasValue)
            .Select(tile => tile.CityId).Distinct());
        Assert.Equal(8, gameMasterTiles.Count(tile => tile.HasResource));
        Assert.Equal(4, playerTiles.Count(tile => tile.HasResource));
        var pathfinder = readScope.ServiceProvider.GetRequiredService<IActorPathfinder>();
        var traveler = npcs.First(npc => npc.ResidentCityId == cities[0].Id);
        var route = await pathfinder.FindAsync(traveler, new Position(first.WorldId, 50, 16));
        Assert.Equal(ActorPathStatus.Found, route.Status);
        Assert.Equal(new Position(first.WorldId, 50, 16), route.Steps[^1]);
        Assert.True(route.Steps.Count > 20);
    }

    [Fact]
    public async Task Campaigns_validate_references_persist_settings_and_end_independently_of_world()
    {
        var options = new DbContextOptionsBuilder<RpgWorldDbContext>()
            .UseNpgsql(_postgres.GetConnectionString()).Options;
        await using var context = new RpgWorldDbContext(options);
        await context.Database.MigrateAsync();
        var world = World.Create("Campaign world", 8, 8, 4);
        context.Worlds.Add(world);
        await context.SaveChangesAsync();
        var modules = new RpgModuleCatalog([new DefaultRpgModule()]);
        var time = new FixedTimeProvider(DateTimeOffset.UnixEpoch);
        var service = new RpgWorld.Infrastructure.Campaigns.CampaignService(context, modules, time);
        var request = new RpgWorld.Application.Campaigns.CreateCampaignRequest(
            "Evening campaign", "rpgworld.default", "{\"language\":\"pt-BR\",\"privateNotes\":true}");
        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.CreateAsync(Guid.NewGuid(), request));
        await Assert.ThrowsAsync<ArgumentException>(() => service.CreateAsync(world.Id, request with { ModuleId = "missing" }));
        await Assert.ThrowsAsync<ArgumentException>(() => service.CreateAsync(world.Id, request with { SettingsJson = "[]" }));
        var first = await service.CreateAsync(world.Id, request);
        var second = await service.CreateAsync(world.Id, request with { Name = "Second game", SettingsJson = "{}" });
        context.ChangeTracker.Clear();
        var loaded = await service.GetAsync(world.Id, first.Id);
        Assert.Equal("pt-BR", loaded.Settings.GetProperty("language").GetString());
        Assert.Equal("rpgworld.default", loaded.ModuleId);
        Assert.Empty(context.ChangeTracker.Entries());
        Assert.Equal(2, (await service.ListAsync(world.Id)).Count);
        Assert.Single(await service.ListAsync(world.Id, limit: 1));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.GetAsync(Guid.NewGuid(), first.Id));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.EndAsync(Guid.NewGuid(), first.Id));
        var ended = await service.EndAsync(world.Id, first.Id);
        Assert.Equal("Ended", ended.Status);
        context.ChangeTracker.Clear();
        Assert.Equal(ended.EndedAtUtc, (await service.EndAsync(world.Id, first.Id)).EndedAtUtc);
        Assert.Equal("Active", (await service.GetAsync(world.Id, second.Id)).Status);
        Assert.True((await context.Worlds.AsNoTracking().SingleAsync()).IsSimulationRunning);
        Assert.Equal(2, await context.Campaigns.CountAsync());
    }

    [Fact]
    public async Task Npc_action_execution_resumes_after_reload_without_another_decision()
    {
        var options = new DbContextOptionsBuilder<RpgWorldDbContext>()
            .UseNpgsql(_postgres.GetConnectionString()).Options;
        var instant = DateTimeOffset.UnixEpoch;
        Guid npcId;
        Guid executionId;
        RpgWorld.Domain.Actors.Actions.NpcActionTarget target;
        await using (var context = new RpgWorldDbContext(options))
        {
            await context.Database.MigrateAsync();
            var world = World.Create("Actions", 16, 16);
            var npc = NpcActor.Create("Traveler", world, world.PositionAt(1, 1), instant);
            target = new(world.PositionAt(9, 7), RpgWorld.Domain.Actors.Actions.NpcActionTargetKind.WorldEntity, Guid.NewGuid());
            npc.SelectAction("Travel", instant, target);
            npcId = npc.Id;
            executionId = npc.ActionExecution!.Id;
            npc.AdvanceAction(executionId, 0.25m, instant.AddMinutes(1));
            context.AddRange(world, npc);
            await context.SaveChangesAsync();
        }
        await using (var context = new RpgWorldDbContext(options))
        {
            var npc = await context.Actors.OfType<NpcActor>().SingleAsync(value => value.Id == npcId);
            Assert.Equal(target, npc.ActionExecution!.Target);
            Assert.Equal(executionId, npc.ActionExecution.Id);
            Assert.Equal(0.25m, npc.ActionExecution.Progress);
            Assert.Equal("Travel", npc.CurrentAction);
            Assert.False(npc.ActionExecution.CanProcess(instant.AddMinutes(1)));
            npc.AdvanceAction(executionId, 0.75m, instant.AddMinutes(2));
            npc.FinishAction(executionId, RpgWorld.Domain.Actors.Actions.NpcActionStatus.Completed, instant.AddMinutes(3));
            await context.SaveChangesAsync();
        }
        await using var read = new RpgWorldDbContext(options);
        var finished = await read.Actors.AsNoTracking().OfType<NpcActor>().SingleAsync(value => value.Id == npcId);
        Assert.Equal(RpgWorld.Domain.Actors.Actions.NpcActionStatus.Completed, finished.ActionExecution!.Status);
        Assert.Equal(1m, finished.ActionExecution.Progress);
        Assert.Null(finished.CurrentAction);
        Assert.Equal(2, await read.WorldEvents.CountAsync(value => value.Type == "NpcActionExecutionChanged"));
    }

    [Fact]
    public async Task Action_pipeline_rolls_back_failures_continues_other_npcs_and_completes_over_multiple_ticks()
    {
        var options = new DbContextOptionsBuilder<RpgWorldDbContext>()
            .UseNpgsql(_postgres.GetConnectionString()).Options;
        await using var context = new RpgWorldDbContext(options);
        await context.Database.MigrateAsync();
        var instant = DateTimeOffset.UnixEpoch;
        var world = World.Create("Execution pipeline", 16, 16);
        var actors = new[] { "throws", "worker", "cancels", "fails", "unregistered" }
            .Select(name => NpcActor.Create(name, world, world.PositionAt(1, 1), instant)).ToArray();
        foreach (var npc in actors) npc.SelectAction(npc.Name == "unregistered" ? "unknown" : "test.action", instant);
        context.Add(world);
        context.AddRange(actors);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        var store = new EfNpcActionExecutionStore(context);
        var executor = new TestNpcActionExecutor(async (step, token) =>
        {
            step.Actor.AddInventory("output", 1, step.Instant);
            step.Actor.Earn(10m, step.Instant);
            await store.SaveChangesAsync(token); // Even an early save must roll back on failure.
            return step.Actor.Name switch
            {
                "throws" => throw new InvalidOperationException("Simulated executor failure."),
                "cancels" => new(NpcActionStepOutcome.Cancel, Reason: "Target changed."),
                "fails" => new(NpcActionStepOutcome.Fail, Reason: "Missing resource."),
                _ => new(step.Execution.Progress == 0m ? NpcActionStepOutcome.Continue : NpcActionStepOutcome.Complete, 0.5m)
            };
        });
        var diagnostics = new NpcActionExecutionDiagnostics();
        var system = new NpcActionExecutionSimulationSystem(store, [executor], diagnostics,
            NullLogger<NpcActionExecutionSimulationSystem>.Instance);
        SimulationTickContext Tick(int minute) => new(world.Id, new WorldClockSnapshot(world.Id,
            instant.AddMinutes(minute), TimeSpan.FromMinutes(1), 1m, instant.AddMinutes(minute)));

        await system.ExecuteAsync(Tick(1));
        await system.ExecuteAsync(Tick(1));
        context.ChangeTracker.Clear();
        var halfway = await context.Actors.OfType<NpcActor>().SingleAsync(npc => npc.Name == "worker");
        Assert.Equal((NpcActionStatus.Running, 0.5m, 1),
            (halfway.ActionExecution!.Status, halfway.ActionExecution.Progress, halfway.InventoryQuantity("output")));
        await system.ExecuteAsync(Tick(2));
        context.ChangeTracker.Clear();
        var final = await context.Actors.AsNoTracking().OfType<NpcActor>().ToArrayAsync();
        foreach (var npc in final)
        {
            Assert.Equal(npc.Name == "worker" ? 2 : 0, npc.InventoryQuantity("output"));
            Assert.Equal(npc.Name == "worker" ? 20m : 0m, npc.Money);
            Assert.Equal(npc.Name switch { "worker" => NpcActionStatus.Completed, "cancels" => NpcActionStatus.Cancelled,
                _ => NpcActionStatus.Failed }, npc.ActionExecution!.Status);
            Assert.Null(npc.CurrentAction);
            Assert.NotNull(diagnostics.GetLatest(npc.Id));
        }
        Assert.Equal(NpcActionStepOutcome.Complete, diagnostics.GetLatest(halfway.Id)!.Outcome);
        Assert.Contains("No executor registered", final.Single(npc => npc.Name == "unregistered").ActionExecution!.Reason);
    }

    [Fact]
    public async Task Cancelling_pipeline_rolls_back_the_tick_and_leaves_action_resumable()
    {
        var options = new DbContextOptionsBuilder<RpgWorldDbContext>()
            .UseNpgsql(_postgres.GetConnectionString()).Options;
        await using var context = new RpgWorldDbContext(options);
        await context.Database.MigrateAsync();
        var instant = DateTimeOffset.UnixEpoch;
        var world = World.Create("Cancelled tick", 8, 8);
        var npc = NpcActor.Create("Worker", world, world.PositionAt(1, 1), instant);
        npc.SelectAction("test.action", instant);
        context.AddRange(world, npc);
        await context.SaveChangesAsync();
        var store = new EfNpcActionExecutionStore(context);
        using var cancellation = new CancellationTokenSource();
        var executor = new TestNpcActionExecutor(async (step, token) =>
        {
            step.Actor.AddInventory("output", 1, step.Instant);
            await store.SaveChangesAsync(token);
            cancellation.Cancel();
            token.ThrowIfCancellationRequested();
            return new(NpcActionStepOutcome.Complete);
        });
        var system = new NpcActionExecutionSimulationSystem(store, [executor], new NpcActionExecutionDiagnostics(),
            NullLogger<NpcActionExecutionSimulationSystem>.Instance);
        var tick = new SimulationTickContext(world.Id, new WorldClockSnapshot(world.Id,
            instant.AddMinutes(1), TimeSpan.FromMinutes(1), 1m, instant));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => system.ExecuteAsync(tick, cancellation.Token));
        var resumed = await store.GetAsync(world.Id, npc.Id);
        Assert.Equal(0, resumed!.InventoryQuantity("output"));
        Assert.Equal(NpcActionStatus.Running, resumed.ActionExecution!.Status);
        Assert.True(resumed.ActionExecution.CanProcess(tick.Clock.CurrentInstant));
    }

    private sealed class TestNpcActionExecutor(
        Func<NpcActionExecutionContext, CancellationToken, Task<NpcActionStepResult>> execute) : INpcActionExecutor
    {
        public string ActionCode => "test.action";
        public Task<NpcActionStepResult> ExecuteAsync(NpcActionExecutionContext context, CancellationToken cancellationToken = default) =>
            execute(context, cancellationToken);
    }

    [Fact]
    public async Task World_admin_inspector_uses_global_summaries_and_filtered_paged_entity_queries()
    {
        var options = new DbContextOptionsBuilder<RpgWorldDbContext>()
            .UseNpgsql(_postgres.GetConnectionString()).Options;
        await using var context = new RpgWorldDbContext(options);
        await context.Database.MigrateAsync();
        var now = DateTimeOffset.UnixEpoch;
        var world = World.Create("Inspectable world", 64, 32, 32);
        var firstChunk = world.CreateChunk(new ChunkCoordinate(0, 0));
        var secondChunk = world.CreateChunk(new ChunkCoordinate(1, 0));
        firstChunk.TransitionSimulationLevel(SimulationLevel.Regional, new RegionAggregateState(20, 30m, 40m, 50m));
        var player = PlayerActor.Create("Player", world, world.PositionAt(1, 1), now);
        var npc = NpcActor.Create("NPC", world, world.PositionAt(2, 1), now);
        var creature = CreatureActor.Create("Creature", world, world.PositionAt(40, 1), now);
        var army = Faction.Create(world, "Army", FactionType.Army, npc.Id, 100m, 30m, now);
        var guild = Faction.Create(world, "Guild", FactionType.Guild, player.Id, 50m, 10m, now);
        npc.JoinFaction(army.Id, now);
        player.JoinFaction(guild.Id, now);
        army.ApplyRelationModifier(guild.Id, new FactionRelationModifier(
            FactionRelationModifierSource.Event, "War for inspector.", tensionDelta: 80), now.AddMinutes(1));
        army.ClaimTerritory(world, [world.PositionAt(4, 4)], now.AddMinutes(1));
        var city = City.Create(world, "Capital", world.PositionAt(3, 3), [world.PositionAt(3, 3)],
            25, 200m, now, army.Id);
        var tile = world.CreateTile(world.PositionAt(4, 4), "temperate", TestDefinitions, 0, 20m, 0.5m);
        var resourceDefinition = new ResourceDefinition("ore", "Ore", "ore", 100m);
        var resource = ResourceDeposit.SpawnOnTile(world, tile, resourceDefinition, now, initialQuantity: 80m);
        context.AddRange(world, firstChunk, secondChunk, player, npc, creature, army, guild, city, tile, resource);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        var service = new WorldAdminService(new EfWorldAdminRepository(context));

        var npcView = await service.InspectAsync(new WorldAdminQuery(
            world.Id, "npcs", PageSize: 10, RegionX: 0, RegionY: 0, FactionId: army.Id));
        var secondChunkPage = await service.InspectAsync(new WorldAdminQuery(
            world.Id, "chunks", Page: 2, PageSize: 1));

        Assert.Equal(3, npcView.Summary.TotalActors);
        Assert.Equal(1, npcView.Summary.Npcs);
        Assert.Equal(1, npcView.Summary.Players);
        Assert.Equal(1, npcView.Summary.Creatures);
        Assert.Equal(1, npcView.Summary.ActiveChunks);
        Assert.Equal(1, npcView.Summary.ResourceDeposits);
        Assert.Equal(80m, npcView.Summary.AvailableResourceQuantity);
        Assert.Equal(25, npcView.Summary.TotalPopulation);
        Assert.Equal(1, npcView.Summary.Armies);
        Assert.Equal(1, npcView.Summary.ActiveWars);
        var npcEntity = Assert.Single(npcView.Entities);
        Assert.Equal(npc.Id, npcEntity.Id);
        Assert.Equal($"/api/actors/{npc.Id}/inspector", npcEntity.DetailPath);
        Assert.Equal(2, secondChunkPage.TotalEntityCount);
        Assert.Equal(1, Assert.Single(secondChunkPage.Entities).RegionX);

        var layerRepository = new EfWorldMapLayerRepository(context, TimeProvider.System);
        var populationLayer = await layerRepository.LoadAsync(new WorldMapLayerQuery(world.Id, WorldMapLayerMode.Population));
        var economyLayer = await layerRepository.LoadAsync(new WorldMapLayerQuery(world.Id, WorldMapLayerMode.Economy));
        var resourceLayer = await layerRepository.LoadAsync(new WorldMapLayerQuery(world.Id, WorldMapLayerMode.Resources));
        var militaryLayer = await layerRepository.LoadAsync(new WorldMapLayerQuery(world.Id, WorldMapLayerMode.Military));
        var temperatureLayer = await layerRepository.LoadAsync(new WorldMapLayerQuery(world.Id, WorldMapLayerMode.Temperature));

        Assert.Equal(1m, Assert.Single(populationLayer!.Cells).Intensity);
        Assert.Equal(1m, Assert.Single(economyLayer!.Cells).Intensity);
        Assert.Equal(resource.Id, Assert.Single(resourceLayer!.Cells).EntityId);
        Assert.Equal(army.Id, Assert.Single(militaryLayer!.Cells).EntityId);
        Assert.Contains("°C", Assert.Single(temperatureLayer!.Cells).Label);
    }

    [Theory]
    [InlineData("png")]
    [InlineData("jpeg")]
    [InlineData("webp")]
    public async Task Imports_supported_image_as_atomic_world_grid(string format)
    {
        var options = new DbContextOptionsBuilder<RpgWorldDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .Options;
        await using var context = new RpgWorldDbContext(options);
        await context.Database.MigrateAsync();
        var bytes = CreateImage(format);
        var importer = new WorldImportService(
            context,
            DefaultWorldDefinitions.Catalog,
            new ColorMapRegionClassifier(),
            TimeProvider.System);

        var result = await importer.ImportAsync(new WorldImportRequest(
            $"Imported {format}",
            $"map.{format}",
            bytes,
            GridResolution: 32));

        Assert.Equal("completed", result.Status);
        Assert.Equal(2, result.Width);
        Assert.Equal(2, result.Height);
        Assert.Equal(1, result.ChunkCount);
        Assert.Equal(4, result.TileCount);
        Assert.Equal(4, await context.Tiles.CountAsync(tile => tile.WorldId == result.WorldId));
        var source = await context.WorldMapSourceImages.SingleAsync(image => image.WorldId == result.WorldId);
        Assert.Equal(bytes, source.Data);
        Assert.Equal(64, source.PixelWidth);
        Assert.Equal(32, source.GridResolution);
    }

    [Fact]
    public async Task Corrupted_import_does_not_leave_partial_world()
    {
        var options = new DbContextOptionsBuilder<RpgWorldDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .Options;
        await using var context = new RpgWorldDbContext(options);
        await context.Database.MigrateAsync();
        var worldsBefore = await context.Worlds.CountAsync();
        var importer = new WorldImportService(
            context,
            DefaultWorldDefinitions.Catalog,
            new ColorMapRegionClassifier(),
            TimeProvider.System);

        await Assert.ThrowsAsync<WorldImportValidationException>(() =>
            importer.ImportAsync(new WorldImportRequest(
                "Broken",
                "broken.png",
                [1, 2, 3, 4, 5],
                GridResolution: 32)));

        Assert.Equal(worldsBefore, await context.Worlds.CountAsync());
    }

    [Fact]
    public async Task Reprocessing_preserves_manual_biome_confirmations()
    {
        var options = new DbContextOptionsBuilder<RpgWorldDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .Options;
        await using var context = new RpgWorldDbContext(options);
        await context.Database.MigrateAsync();
        var classifier = new ColorMapRegionClassifier();
        var importer = new WorldImportService(
            context,
            DefaultWorldDefinitions.Catalog,
            classifier,
            TimeProvider.System);
        var imported = await importer.ImportAsync(new WorldImportRequest(
            "Reviewable world",
            "map.png",
            CreateImage("png"),
            GridResolution: 32));
        var service = new WorldClassificationService(
            context,
            DefaultWorldDefinitions.Catalog,
            classifier);

        await service.ConfirmManualAsync(imported.WorldId, 0, 0, "snow");
        var result = await service.ReprocessAsync(imported.WorldId);
        context.ChangeTracker.Clear();

        var tiles = await context.Tiles
            .AsNoTracking()
            .Where(tile => tile.WorldId == imported.WorldId)
            .OrderBy(tile => tile.Y)
            .ThenBy(tile => tile.X)
            .ToArrayAsync();
        Assert.Equal(3, result.AutomaticallyClassified);
        Assert.Equal(1, result.PreservedManual);
        Assert.Equal("snow", tiles[0].BiomeCode);
        Assert.Equal(BiomeClassificationOrigin.Manual, tiles[0].BiomeClassificationOrigin);
        Assert.All(tiles[1..], tile =>
        {
            Assert.Equal("forest", tile.BiomeCode);
            Assert.Equal(BiomeClassificationOrigin.Automatic, tile.BiomeClassificationOrigin);
            Assert.NotNull(tile.BiomeClassificationConfidence);
        });
    }

    [Fact]
    public async Task Paints_multiple_tiles_and_persists_undo_redo_history()
    {
        var options = new DbContextOptionsBuilder<RpgWorldDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .Options;
        await using var context = new RpgWorldDbContext(options);
        await context.Database.MigrateAsync();
        var classifier = new ColorMapRegionClassifier();
        var imported = await new WorldImportService(
            context,
            DefaultWorldDefinitions.Catalog,
            classifier,
            TimeProvider.System).ImportAsync(new WorldImportRequest(
                "Editable world",
                "map.png",
                CreateImage("png"),
                GridResolution: 32));
        var editor = new MapEditingService(
            context,
            DefaultWorldDefinitions.Catalog,
            TimeProvider.System);

        var painted = await editor.PaintAsync(
            imported.WorldId,
            new MapPaintRequest(MapBrushKind.Desert, CenterX: 0, CenterY: 0, Size: 2));
        Assert.Equal(4, painted.AffectedTiles);
        context.ChangeTracker.Clear();
        var paintedTiles = await context.Tiles
            .Where(tile => tile.WorldId == imported.WorldId)
            .ToArrayAsync();
        Assert.All(paintedTiles, tile =>
        {
            Assert.Equal("desert", tile.BiomeCode);
            Assert.Equal(BiomeClassificationOrigin.Manual, tile.BiomeClassificationOrigin);
        });

        await editor.UndoAsync(imported.WorldId);
        context.ChangeTracker.Clear();
        var undoneTiles = await context.Tiles
            .Where(tile => tile.WorldId == imported.WorldId)
            .ToArrayAsync();
        Assert.All(undoneTiles, tile => Assert.Equal("forest", tile.BiomeCode));

        await editor.RedoAsync(imported.WorldId);
        context.ChangeTracker.Clear();
        Assert.Equal(
            4,
            await context.Tiles.CountAsync(tile =>
                tile.WorldId == imported.WorldId &&
                tile.BiomeCode == "desert" &&
                tile.BiomeClassificationOrigin == BiomeClassificationOrigin.Manual));

        await editor.PaintAsync(
            imported.WorldId,
            new MapPaintRequest(MapBrushKind.City, CenterX: 0, CenterY: 0, Size: 1));
        context.ChangeTracker.Clear();
        Assert.NotNull((await context.Tiles.SingleAsync(tile =>
            tile.WorldId == imported.WorldId && tile.X == 0 && tile.Y == 0)).StructureId);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            editor.PaintAsync(
                imported.WorldId,
                new MapPaintRequest(MapBrushKind.Forest, 0, 0, Size: 17)));
    }

    [Fact]
    public async Task World_clock_survives_context_restart_with_current_instant()
    {
        var options = new DbContextOptionsBuilder<RpgWorldDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .Options;
        var initial = new DateTimeOffset(2030, 5, 10, 9, 0, 0, TimeSpan.Zero);
        var world = World.Create("Clockwork", 8, 8);

        await using (var firstContext = new RpgWorldDbContext(options))
        {
            await firstContext.Database.MigrateAsync();
            firstContext.Worlds.Add(world);
            firstContext.WorldClocks.Add(WorldClock.Create(
                world.Id,
                initial,
                initial,
                tickDuration: TimeSpan.FromHours(1),
                realTimeMultiplier: 3m));
            await firstContext.SaveChangesAsync();
        }

        await using (var secondContext = new RpgWorldDbContext(options))
        {
            var repository = new EfWorldClockRepository(secondContext);
            var clock = await repository.GetAsync(world.Id);
            Assert.NotNull(clock);
            clock.AdvanceTicks(2);
            await repository.SaveChangesAsync();
        }

        await using var restartedContext = new RpgWorldDbContext(options);
        var restored = await restartedContext.WorldClocks.AsNoTracking().SingleAsync(clock => clock.WorldId == world.Id);
        Assert.Equal(initial.AddHours(2), restored.CurrentInstant);
        Assert.Equal(TimeSpan.FromHours(1), restored.TickDuration);
        Assert.Equal(3m, restored.RealTimeMultiplier);
    }

    [Fact]
    public async Task Game_master_commands_create_move_adjust_and_emit_events_end_to_end()
    {
        var options = new DbContextOptionsBuilder<RpgWorldDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .Options;
        await using var context = new RpgWorldDbContext(options);
        await context.Database.MigrateAsync();
        var now = new DateTimeOffset(2035, 1, 2, 3, 0, 0, TimeSpan.Zero);
        var world = World.Create("Directed world", 8, 8);
        var origin = world.CreateTile(world.PositionAt(1, 1), "forest", DefaultWorldDefinitions.Catalog, 0, 20m, 0.5m);
        var destination = world.CreateTile(world.PositionAt(2, 2), "forest", DefaultWorldDefinitions.Catalog, 0, 20m, 0.5m);
        var resourceTile = world.CreateTile(world.PositionAt(3, 3), "forest", DefaultWorldDefinitions.Catalog, 0, 20m, 0.5m);
        var deposit = ResourceDeposit.SpawnOnTile(
            world, resourceTile, DefaultWorldDefinitions.Catalog.ResolveResource("wood"), now,
            initialQuantity: 20m, capacity: 100m);
        context.AddRange(world, origin, destination, resourceTile, deposit);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        var publisher = new RecordingWorldUpdatePublisher();
        var service = new GameMasterCommandService(
            context, DefaultWorldDefinitions.Catalog, publisher, new FixedTimeProvider(now));

        var created = await service.ExecuteAsync(world.Id, new GameMasterCommand(
            GameMasterCommandType.CreateNpc, now.AddMinutes(1), Name: "Guide", X: 1, Y: 1));
        await service.ExecuteAsync(world.Id, new GameMasterCommand(
            GameMasterCommandType.MoveActor, now.AddMinutes(2), ActorId: created.EntityId, X: 2, Y: 2));
        await service.ExecuteAsync(world.Id, new GameMasterCommand(
            GameMasterCommandType.AdjustResource, now.AddMinutes(3), ResourceDepositId: deposit.Id,
            ResourceQuantityDelta: 5m));
        var authoredEvent = await service.ExecuteAsync(world.Id, new GameMasterCommand(
            GameMasterCommandType.CreateEvent, now.AddMinutes(4), ActorId: created.EntityId,
            X: 2, Y: 2, EventType: "story.omen", EventPayload: "{\"sign\":\"red-moon\"}"));
        context.ChangeTracker.Clear();

        var npc = await context.Actors.AsNoTracking().OfType<NpcActor>().SingleAsync(value => value.Id == created.EntityId);
        var storedDeposit = await context.ResourceDeposits.AsNoTracking().SingleAsync(value => value.Id == deposit.Id);
        var auditEvents = await context.WorldEvents.AsNoTracking().Where(value => value.WorldId == world.Id &&
            value.Type.StartsWith("game-master.")).OrderBy(value => value.TimestampUtc).ToArrayAsync();
        var storyEvent = await context.WorldEvents.AsNoTracking().SingleAsync(value => value.Id == authoredEvent.EntityId);

        Assert.Equal((2, 2), (npc.X, npc.Y));
        Assert.Equal(25m, storedDeposit.Quantity);
        Assert.Equal(4, auditEvents.Length);
        Assert.All(auditEvents, value => Assert.Contains("game-master-command", value.Payload));
        Assert.Equal("story.omen", storyEvent.Type);
        Assert.Equal(8, publisher.Messages.Count);
        Assert.All(publisher.Messages, value => Assert.Equal("game-master-command", value.UpdateType));
    }

    [Fact]
    public async Task Campaign_simulation_settings_persist_audit_and_update_world_speed()
    {
        var options = new DbContextOptionsBuilder<RpgWorldDbContext>()
            .UseNpgsql(_postgres.GetConnectionString()).Options;
        var now = new DateTimeOffset(2038, 1, 2, 3, 0, 0, TimeSpan.Zero);
        Guid worldId;
        await using (var context = new RpgWorldDbContext(options))
        {
            await context.Database.MigrateAsync();
            var world = World.Create("Configured campaign", 8, 8);
            context.Worlds.Add(world);
            await context.SaveChangesAsync();
            var service = new CampaignSimulationSettingsService(context, new FixedTimeProvider(now));

            var defaults = await service.GetEffectiveAsync(world.Id);
            var updated = await service.UpdateAsync(world.Id, new UpdateCampaignSimulationSettings(
                0.5m, 2m, 3m, 1.5m, 2m, 0.8m, 1.2m, 4m));

            Assert.Equal((1m, 1m, 1m), (defaults.NPCDensity, defaults.ResourceScarcity, defaults.SimulationSpeed));
            Assert.Equal((0.5m, 2m, 4m), (updated.NPCDensity, updated.ResourceScarcity, updated.SimulationSpeed));
            Assert.Equal(4m, (await context.WorldClocks.SingleAsync(value => value.WorldId == world.Id)).RealTimeMultiplier);
            Assert.Contains(await context.WorldEvents.Where(value => value.WorldId == world.Id).ToArrayAsync(),
                value => value.Type == "campaign.settings.changed" && value.Payload.Contains("ResourceScarcity"));
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => service.UpdateAsync(world.Id,
                new UpdateCampaignSimulationSettings(0m, 1m, 1m, 1m, 1m, 1m, 1m, 1m)));
            worldId = world.Id;
        }

        await using var restarted = new RpgWorldDbContext(options);
        var restored = await new CampaignSimulationSettingsService(restarted,
            new FixedTimeProvider(now.AddMinutes(1))).GetEffectiveAsync(worldId);
        Assert.Equal((0.5m, 2m, 4m), (restored.NPCDensity, restored.ResourceScarcity, restored.SimulationSpeed));
        Assert.Equal(2, restored.Version);
    }

    [Fact]
    public async Task Custom_content_is_campaign_scoped_persistent_validated_and_overrides_modules()
    {
        var options = new DbContextOptionsBuilder<RpgWorldDbContext>()
            .UseNpgsql(_postgres.GetConnectionString()).Options;
        var now = new DateTimeOffset(2037, 1, 2, 3, 0, 0, TimeSpan.Zero);
        var modules = new RpgModuleCatalog([new DefaultRpgModule()]).Load(["rpgworld.default"]);
        Guid worldId;
        Guid creatureId;

        await using (var context = new RpgWorldDbContext(options))
        {
            await context.Database.MigrateAsync();
            var world = World.Create("Homebrew campaign", 8, 8);
            var otherWorld = World.Create("Other campaign", 8, 8);
            context.AddRange(world, otherWorld);
            await context.SaveChangesAsync();
            var service = new CustomContentService(context, modules, new FixedTimeProvider(now));
            var creature = await service.CreateAsync(world.Id, new CustomContentRequest(
                CustomContentKind.Creature, "wolf", "Dire Wolf", "{\"maximumHealth\":220,\"tags\":[\"homebrew\"]}"));
            await service.CreateAsync(world.Id, new CustomContentRequest(
                CustomContentKind.Item, "moon-key", "Moon Key", "{\"category\":\"key\",\"stackable\":false}"));
            await service.CreateAsync(world.Id, new CustomContentRequest(
                CustomContentKind.Biome, "ashen-forest", "Ashen Forest",
                "{\"terrainCode\":\"woodland\",\"minimumTemperatureCelsius\":-10," +
                "\"maximumTemperatureCelsius\":40,\"minimumHumidity\":0.2,\"maximumHumidity\":0.9}"));
            await service.CreateAsync(world.Id, new CustomContentRequest(
                CustomContentKind.Rule, "movement", "Homebrew Movement", "{\"parameters\":{\"diagonal-cost\":1.5}}"));
            await service.CreateAsync(world.Id, new CustomContentRequest(
                CustomContentKind.Npc, "guide", "Campaign Guide", "{\"maximumHealth\":90,\"defaultJob\":\"guide\"}"));
            foreach (var kind in new[] { CustomContentKind.Class,
                         CustomContentKind.Faction, CustomContentKind.Event })
                await service.CreateAsync(world.Id, new CustomContentRequest(
                    kind, $"custom-{kind.ToString().ToLowerInvariant()}", $"Custom {kind}", "{\"notes\":\"versionable\"}"));
            await service.CreateAsync(otherWorld.Id, new CustomContentRequest(
                CustomContentKind.Creature, "wolf", "Small Wolf", "{\"maximumHealth\":20}"));

            Assert.Equal(8, (await service.ListAsync(world.Id)).Count);
            Assert.Single(await service.ListAsync(otherWorld.Id));
            var catalog = await service.ResolveCatalogAsync(world.Id);
            Assert.Equal(("Dire Wolf", 220), (catalog.ResolveCreature("wolf").Name, catalog.ResolveCreature("wolf").MaximumHealth));
            Assert.Equal("moon-key", catalog.ResolveItem("moon-key").Code);
            Assert.Equal("guide", catalog.ResolveNpc("guide").DefaultJob);
            Assert.Equal("ashen-forest", catalog.ResolveBiome("ashen-forest").Code);
            Assert.Equal(1.5m, catalog.ResolveRule("movement").Parameters["diagonal-cost"]);
            await Assert.ThrowsAsync<ArgumentException>(() => service.CreateAsync(world.Id, new CustomContentRequest(
                CustomContentKind.Biome, "invalid", "Invalid", "{\"terrainCode\":\"missing\"}")));
            var updated = await service.UpdateAsync(world.Id, creature.Id,
                new UpdateCustomContentRequest("Dire Wolf Prime", "{\"maximumHealth\":300}"));
            Assert.Equal(2, updated.Version);
            Assert.Equal(8, (await service.ExportAsync(world.Id)).Definitions.Count);
            worldId = world.Id;
            creatureId = creature.Id;
        }

        await using var restarted = new RpgWorldDbContext(options);
        var restored = new CustomContentService(restarted, modules, new FixedTimeProvider(now.AddMinutes(1)));
        Assert.Equal("Dire Wolf Prime", (await restored.ListAsync(worldId)).Single(value => value.Id == creatureId).Name);
        Assert.Equal(300, (await restored.ResolveCatalogAsync(worldId)).ResolveCreature("wolf").MaximumHealth);
        var eventDefinition = (await restored.ListAsync(worldId)).Single(value => value.Kind == CustomContentKind.Event);
        await restored.DeleteAsync(worldId, eventDefinition.Id);
        Assert.DoesNotContain(await restored.ListAsync(worldId), value => value.Id == eventDefinition.Id);
    }

    [Fact]
    public async Task Player_fog_persists_history_and_never_serializes_unknown_tile_secrets()
    {
        var options = new DbContextOptionsBuilder<RpgWorldDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .Options;
        var now = new DateTimeOffset(2036, 2, 3, 4, 0, 0, TimeSpan.Zero);
        Guid worldId;
        Guid playerId;

        await using (var context = new RpgWorldDbContext(options))
        {
            await context.Database.MigrateAsync();
            var world = World.Create("Hidden world", 8, 8);
            var chunk = world.CreateChunk(new ChunkCoordinate(0, 0));
            var tiles = (from y in Enumerable.Range(0, 8)
                from x in Enumerable.Range(0, 8)
                select world.CreateTile(world.PositionAt(x, y), "forest", DefaultWorldDefinitions.Catalog, 0, 20m, 0.5m))
                .ToArray();
            var player = PlayerActor.Create("Explorer", world, world.PositionAt(2, 2), now);
            tiles.Single(value => value.X == 2 && value.Y == 2).AddOccupant(player.Id);
            var visibleTile = tiles.Single(value => value.X == 5 && value.Y == 5);
            var secretTile = tiles.Single(value => value.X == 0 && value.Y == 7);
            var visibleNpc = NpcActor.Create("Visible NPC", world, world.PositionAt(6, 5), now);
            visibleNpc.AssignJob("merchant", now);
            var visibleGuard = NpcActor.Create("Visible Guard", world, world.PositionAt(5, 4), now);
            visibleGuard.AssignJob("city guard", now);
            var visibleCreature = CreatureActor.Create("Visible Creature", world, world.PositionAt(5, 6), now);
            var visiblePlayer = PlayerActor.Create("Visible Player", world, world.PositionAt(3, 5), now);
            var hiddenNpc = NpcActor.Create("Hidden NPC", world, secretTile.Position, now);
            var visibleStructureId = Guid.NewGuid();
            var secretStructureId = Guid.NewGuid();
            tiles.Single(value => value.X == 6 && value.Y == 6).AssignStructure(visibleStructureId);
            secretTile.AssignStructure(secretStructureId);
            var visibleResource = ResourceDeposit.SpawnOnTile(
                world, visibleTile, DefaultWorldDefinitions.Catalog.ResolveResource("wood"), now, initialQuantity: 30m);
            var secretResource = ResourceDeposit.SpawnOnTile(
                world, secretTile, DefaultWorldDefinitions.Catalog.ResolveResource("stone"), now, initialQuantity: 40m);
            visibleResource.Discover(player.Id, now);
            secretResource.Discover(player.Id, now);
            var visibleCity = City.Create(world, "Seen City", visibleTile.Position,
                [visibleTile.Position, world.PositionAt(6, 5), world.PositionAt(5, 6), world.PositionAt(5, 4),
                    world.PositionAt(3, 5), world.PositionAt(4, 5), world.PositionAt(6, 6)], 10, 5m, now);
            var secretCity = City.Create(world, "Secret City", secretTile.Position, [secretTile.Position], 10, 5m, now);
            var visibleEvent = WorldEvent.Create(Guid.NewGuid(), world.Id, "story.visible", now,
                new WorldEventPosition(5, 5), [visibleNpc.Id], "{\"secret\":\"safe\"}");
            var hiddenEvent = WorldEvent.Create(Guid.NewGuid(), world.Id, "story.hidden", now,
                new WorldEventPosition(0, 7), [hiddenNpc.Id], "{\"secret\":\"hidden\"}");
            context.AddRange(world, chunk, player, visibleNpc, visibleGuard, visibleCreature, visiblePlayer, hiddenNpc,
                visibleResource, secretResource, visibleCity, secretCity, visibleEvent, hiddenEvent);
            context.Tiles.AddRange(tiles);
            await context.SaveChangesAsync();
            context.ChangeTracker.Clear();
            var visibility = new PlayerVisibilityService(context, new FixedTimeProvider(now));
            await visibility.GetAsync(player.Id);
            var trackedPlayer = await context.Actors.OfType<PlayerActor>().SingleAsync(value => value.Id == player.Id);
            trackedPlayer.Move(world, world.PositionAt(5, 5), now.AddMinutes(1));
            var movement = Assert.IsType<ActorMovedEvent>(Assert.Single(trackedPlayer.DomainEvents));
            await context.SaveChangesAsync();
            await new PlayerVisibilityMovedEventHandler(context, visibility).HandleAsync(movement);
            var map = await new PlayerWorldMapProvider(context, visibility).GetMapAsync(world.Id, player.Id);

            Assert.NotNull(map);
            var exposed = map.Chunks.SelectMany(value => value.Tiles).ToArray();
            Assert.DoesNotContain(exposed, value => value.X == 0 && value.Y == 7);
            Assert.Equal("Known", exposed.Single(value => value.X == 2 && value.Y == 2).KnowledgeState);
            Assert.Equal("Discovered", exposed.Single(value => value.X == 0 && value.Y == 0).KnowledgeState);
            var currentlyVisible = exposed.Single(value => value.X == 5 && value.Y == 5);
            Assert.Equal("Visible", currentlyVisible.KnowledgeState);
            Assert.Equal("wood", currentlyVisible.ResourceCode);
            Assert.Equal("Seen City", currentlyVisible.CityName);
            Assert.DoesNotContain("Secret City", System.Text.Json.JsonSerializer.Serialize(map));
            Assert.Equal(46, exposed.Length);
            var playerView = await new PlayerWorldViewService(context, visibility).GetAsync(player.Id);
            Assert.Equal(4, playerView.VisibleEntities.Count);
            Assert.Contains(playerView.VisibleEntities, value => value.Kind == "npc" && value.Name == "Visible NPC");
            Assert.Contains(playerView.VisibleEntities, value => value.Kind == "creature" && value.Name == "Visible Creature");
            Assert.Contains(playerView.VisibleEntities, value => value.Kind == "player" && value.Name == "Visible Player");
            Assert.DoesNotContain(playerView.VisibleEntities, value => value.Name == "Hidden NPC");
            Assert.Contains(playerView.VisibleStructures, value => value.Id == visibleStructureId);
            Assert.DoesNotContain(playerView.VisibleStructures, value => value.Id == secretStructureId);
            Assert.Contains(playerView.RelevantEvents, value => value.Id == visibleEvent.Id);
            Assert.DoesNotContain(playerView.RelevantEvents, value => value.Id == hiddenEvent.Id);
            var serializedPlayerView = System.Text.Json.JsonSerializer.Serialize(playerView);
            Assert.DoesNotContain("Hidden NPC", serializedPlayerView);
            Assert.DoesNotContain("health", serializedPlayerView, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("payload", serializedPlayerView, StringComparison.OrdinalIgnoreCase);
            var currentRegionService = new PlayerCurrentRegionService(context,
                new PlayerWorldViewService(context, visibility));
            var currentRegion = await currentRegionService.GetAsync(player.Id);
            Assert.Equal((visibleCity.Id, "city", "Seen City"),
                (currentRegion.RegionId, currentRegion.RegionKind, currentRegion.RegionName));
            Assert.Equal("merchant", currentRegion.VisibleEntities.Single(value => value.Id == visibleNpc.Id).Category);
            Assert.Equal("guard", currentRegion.VisibleEntities.Single(value => value.Id == visibleGuard.Id).Category);
            Assert.Equal(visibleGuard.Id, currentRegion.VisibleEntities[0].Id);
            Assert.Contains(currentRegion.VisibleEntities, value => value.Kind == "player");
            Assert.Contains(currentRegion.VisibleEntities, value => value.Kind == "creature");
            Assert.Contains(currentRegion.VisibleEstablishments, value => value.Id == visibleStructureId);
            Assert.Contains(currentRegion.RelevantEvents, value => value.Id == visibleEvent.Id);
            Assert.DoesNotContain("Hidden NPC", System.Text.Json.JsonSerializer.Serialize(currentRegion));
            var recipients = await visibility.ListPlayersSeeingAsync(world.Id, 5, 5);
            Assert.Contains(player.Id, recipients);
            Assert.Contains(visiblePlayer.Id, recipients);
            Assert.DoesNotContain(hiddenNpc.Id, recipients);
            trackedPlayer.Move(world, world.PositionAt(2, 2), now.AddMinutes(2));
            var returnMovement = Assert.IsType<ActorMovedEvent>(Assert.Single(trackedPlayer.DomainEvents));
            await context.SaveChangesAsync();
            await new PlayerVisibilityMovedEventHandler(context, visibility).HandleAsync(returnMovement);
            var changedRegion = await currentRegionService.GetAsync(player.Id);
            Assert.Equal("chunk", changedRegion.RegionKind);
            Assert.Equal(chunk.Id, changedRegion.RegionId);
            worldId = world.Id;
            playerId = player.Id;
        }

        await using var restarted = new RpgWorldDbContext(options);
        var restoredVisibility = await new PlayerVisibilityService(restarted, new FixedTimeProvider(now.AddMinutes(2)))
            .GetAsync(playerId);
        Assert.Equal(worldId, restoredVisibility.WorldId);
        Assert.Equal("Visible", restoredVisibility.Tiles.Single(value => value.X == 2 && value.Y == 2).State);
        Assert.DoesNotContain(restoredVisibility.Tiles, value => value.X == 0 && value.Y == 7);
    }

    private static byte[] CreateImage(string format)
    {
        using var image = new Image<Rgba32>(64, 64, new Rgba32(30, 150, 60));
        using var stream = new MemoryStream();
        IImageEncoder encoder = format switch
        {
            "png" => new PngEncoder(),
            "jpeg" => new JpegEncoder(),
            "webp" => new WebpEncoder(),
            _ => throw new ArgumentOutOfRangeException(nameof(format))
        };
        image.Save(stream, encoder);
        return stream.ToArray();
    }

    private sealed record MerchantDeathOutcome(
        int OperatingRoutes,
        int FinalRoutes,
        decimal OperatingStock,
        decimal FinalStock,
        decimal OperatingPrice,
        decimal FinalPrice,
        decimal OperatingSatisfaction,
        decimal FinalSatisfaction,
        string EventTypes);

    private sealed class FixedTimeProvider(DateTimeOffset value) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => value;
    }

    private sealed class RecordingWorldUpdatePublisher : IWorldUpdatePublisher
    {
        public List<WorldUpdateMessage> Messages { get; } = [];

        public Task PublishToWorldAsync(WorldUpdateMessage message, CancellationToken cancellationToken = default)
        {
            Messages.Add(message);
            return Task.CompletedTask;
        }

        public Task PublishToChunkAsync(Guid chunkId, WorldUpdateMessage message, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task PublishToPlayerAsync(Guid playerId, WorldUpdateMessage message, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task PublishToGameMasterAsync(WorldUpdateMessage message, CancellationToken cancellationToken = default)
        {
            Messages.Add(message);
            return Task.CompletedTask;
        }
    }
}

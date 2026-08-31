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
using RpgWorld.Domain.Worlds.Resources;
using RpgWorld.Simulation.Worlds.Resources;

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
}

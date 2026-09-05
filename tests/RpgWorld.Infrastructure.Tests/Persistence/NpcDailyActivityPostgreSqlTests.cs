using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RpgWorld.Domain.Actors;
using RpgWorld.Domain.Actors.Actions;
using RpgWorld.Domain.Worlds;
using RpgWorld.Domain.Worlds.Cities;
using RpgWorld.Domain.Worlds.Definitions;
using RpgWorld.Domain.Worlds.Resources;
using RpgWorld.Infrastructure.Persistence;
using RpgWorld.Simulation.Actors.Actions;
using RpgWorld.Simulation.Actors.Utility;
using RpgWorld.Simulation.Engine;
using RpgWorld.Simulation.Time;

namespace RpgWorld.Infrastructure.Tests.Persistence;

public sealed partial class RpgWorldDbContextPostgreSqlTests
{
    [Theory]
    [InlineData("Eat")]
    [InlineData("Sleep")]
    [InlineData("Work")]
    public async Task Daily_actions_are_chosen_walk_and_apply_persisted_effects_once(string action)
    {
        await using var provider = CreateTravelProvider(new TravelTimeProvider(), new RecordingWorldUpdatePublisher());
        var (worldId, npcId, _) = await SeedTravelAsync(provider);
        await ConfigureDailyActorAsync(provider, worldId, npcId, action, true);
        var instant = DateTimeOffset.UnixEpoch.AddHours(1);
        var positions = new List<Position>();
        Guid? executionId = null;
        decimal? previousEnergy = null;
        var recoveredOverMultipleTicks = 0;
        for (var index = 0; index < 20; index++)
        {
            // The real decision and execution systems run in fresh scopes, without player commands.
            await RunDailyTickAsync(provider, worldId, instant, decide: true);
            await using var scope = provider.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<RpgWorldDbContext>();
            var npc = await db.Actors.AsNoTracking().OfType<NpcActor>().SingleAsync(value => value.Id == npcId);
            Assert.Equal(action, npc.ActionExecution!.ActionCode);
            executionId ??= npc.ActionExecution.Id;
            Assert.Equal(executionId, npc.ActionExecution.Id);
            Assert.True(npc.ActionExecution.Status is NpcActionStatus.Running or NpcActionStatus.Completed,
                npc.ActionExecution.Reason);
            positions.Add(npc.Position);
            if (previousEnergy is { } energy && npc.Energy > energy) recoveredOverMultipleTicks++;
            previousEnergy = npc.Energy;
            // Reprocessing the exact instant must not repeat movement, resource use, recovery or payment.
            await RunDailyTickAsync(provider, worldId, instant, decide: true);
            var duplicate = await db.Actors.AsNoTracking().OfType<NpcActor>().SingleAsync(value => value.Id == npcId);
            Assert.Equal((npc.Position, npc.Hunger, npc.Energy, npc.Money, npc.ActionExecution),
                (duplicate.Position, duplicate.Hunger, duplicate.Energy, duplicate.Money, duplicate.ActionExecution));
            if (npc.ActionExecution.Status == NpcActionStatus.Completed)
            {
                Assert.Equal(new Position(worldId, 6, 1), npc.Position);
                Assert.True(positions.Distinct().Count() >= 5);
                if (action == "Eat")
                {
                    Assert.InRange(npc.Hunger, 55m, 56m);
                    Assert.Equal(2m, (await db.ResourceDeposits.AsNoTracking().SingleAsync()).Quantity);
                    Assert.Equal(0, npc.InventoryQuantity("food"));
                }
                else if (action == "Sleep")
                {
                    Assert.True(npc.Energy >= 99m);
                    Assert.True(recoveredOverMultipleTicks > 1);
                }
                else
                {
                    Assert.Equal(2m, npc.Money);
                    var city = await db.Cities.AsNoTracking().SingleAsync();
                    Assert.Equal(98m, city.Wealth);
                    Assert.Equal(1m, city.ResourceStocks["food"]);
                }
                return;
            }
            instant = npc.X == 6 && action != "Eat" ? instant.AddMinutes(30) : instant.AddSeconds(1);
        }
        Assert.Fail($"{action} did not complete.");
    }

    [Theory]
    [InlineData("Eat")]
    [InlineData("Sleep")]
    [InlineData("Work")]
    public async Task Daily_actions_fail_without_a_valid_resource_or_site(string action)
    {
        await using var provider = CreateTravelProvider(new TravelTimeProvider(), new RecordingWorldUpdatePublisher());
        var (worldId, npcId, _) = await SeedTravelAsync(provider);
        await ConfigureDailyActorAsync(provider, worldId, npcId, action, false);
        await RunDailyTickAsync(provider, worldId, DateTimeOffset.UnixEpoch.AddHours(1), decide: false);
        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<RpgWorldDbContext>();
        var npc = await db.Actors.AsNoTracking().OfType<NpcActor>().SingleAsync(value => value.Id == npcId);
        Assert.Equal(NpcActionStatus.Failed, npc.ActionExecution!.Status);
        Assert.NotEmpty(npc.ActionExecution.Reason!);
        Assert.Equal(new Position(worldId, 1, 1), npc.Position);
        Assert.Equal(0m, npc.Money);
        Assert.Equal(action == "Eat" ? 90m : 0m, npc.Hunger);
        Assert.Equal(action == "Sleep" ? 10m : 100m, npc.Energy);
    }

    [Theory]
    [InlineData("Eat")]
    [InlineData("Sleep")]
    [InlineData("Work")]
    public async Task Interrupted_daily_actions_do_not_apply_later_effects(string action)
    {
        await using var provider = CreateTravelProvider(new TravelTimeProvider(), new RecordingWorldUpdatePublisher());
        var (worldId, npcId, _) = await SeedTravelAsync(provider);
        await ConfigureDailyActorAsync(provider, worldId, npcId, action, true);
        await RunDailyTickAsync(provider, worldId, DateTimeOffset.UnixEpoch.AddHours(1), decide: true);
        await using (var scope = provider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RpgWorldDbContext>();
            var npc = await db.Actors.OfType<NpcActor>().SingleAsync(value => value.Id == npcId);
            npc.TakeDamage(1, null, DateTimeOffset.UnixEpoch.AddHours(1).AddSeconds(1));
            await db.SaveChangesAsync();
        }
        await RunDailyTickAsync(provider, worldId, DateTimeOffset.UnixEpoch.AddHours(2), decide: false);
        await using var readScope = provider.CreateAsyncScope();
        var read = readScope.ServiceProvider.GetRequiredService<RpgWorldDbContext>();
        var interrupted = await read.Actors.AsNoTracking().OfType<NpcActor>().SingleAsync(value => value.Id == npcId);
        Assert.Equal(NpcActionStatus.Cancelled, interrupted.ActionExecution!.Status);
        Assert.Equal(2, interrupted.X);
        Assert.Equal(0m, interrupted.Money);
        if (action == "Eat") Assert.Equal(3m, (await read.ResourceDeposits.SingleAsync()).Quantity);
        if (action == "Sleep") Assert.Equal(10m, interrupted.Energy);
        if (action == "Work") Assert.Empty((await read.Cities.SingleAsync()).ResourceStocks);
    }

    [Theory]
    [InlineData("Sleep")]
    [InlineData("Work")]
    public async Task Unsafe_daily_actions_cancel_before_movement_or_effects(string action)
    {
        await using var provider = CreateTravelProvider(new TravelTimeProvider(), new RecordingWorldUpdatePublisher());
        var (worldId, npcId, playerId) = await SeedTravelAsync(provider);
        await ConfigureDailyActorAsync(provider, worldId, npcId, action, true);
        await using (var scope = provider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RpgWorldDbContext>();
            var npc = await db.Actors.OfType<NpcActor>().SingleAsync(value => value.Id == npcId);
            npc.SetRelationship(playerId, "enemy", -100, DateTimeOffset.UnixEpoch);
            npc.SelectAction(action, DateTimeOffset.UnixEpoch);
            await db.SaveChangesAsync();
        }
        await RunDailyTickAsync(provider, worldId, DateTimeOffset.UnixEpoch, decide: false);
        await using var readScope = provider.CreateAsyncScope();
        var read = readScope.ServiceProvider.GetRequiredService<RpgWorldDbContext>();
        var cancelled = await read.Actors.AsNoTracking().OfType<NpcActor>().SingleAsync(value => value.Id == npcId);
        Assert.Equal(NpcActionStatus.Cancelled, cancelled.ActionExecution!.Status);
        Assert.Equal(1, cancelled.X);
        Assert.Equal(0m, cancelled.Money);
        if (action == "Sleep") Assert.Equal(10m, cancelled.Energy);
    }

    [Fact]
    public async Task Eating_inventory_food_does_not_extract_another_source_or_repeat_the_tick()
    {
        await using var provider = CreateTravelProvider(new TravelTimeProvider(), new RecordingWorldUpdatePublisher());
        var (worldId, npcId, _) = await SeedTravelAsync(provider);
        await ConfigureDailyActorAsync(provider, worldId, npcId, "Eat", true);
        var instant = DateTimeOffset.UnixEpoch.AddHours(1);
        await using (var scope = provider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RpgWorldDbContext>();
            var npc = await db.Actors.OfType<NpcActor>().SingleAsync(value => value.Id == npcId);
            npc.AddInventory("ration", 3, instant);
            await db.SaveChangesAsync();
        }
        await RunDailyTickAsync(provider, worldId, instant, decide: true);
        await RunDailyTickAsync(provider, worldId, instant, decide: true);
        await using var readScope = provider.CreateAsyncScope();
        var read = readScope.ServiceProvider.GetRequiredService<RpgWorldDbContext>();
        var fed = await read.Actors.AsNoTracking().OfType<NpcActor>().SingleAsync(value => value.Id == npcId);
        Assert.Equal(NpcActionStatus.Completed, fed.ActionExecution!.Status);
        Assert.Equal(55m, fed.Hunger);
        Assert.Equal(2, fed.InventoryQuantity("ration"));
        Assert.Equal(1, fed.X);
        Assert.Equal(3m, (await read.ResourceDeposits.SingleAsync()).Quantity);
    }

    private static async Task RunDailyTickAsync(ServiceProvider provider, Guid worldId, DateTimeOffset instant, bool decide)
    {
        await using var scope = provider.CreateAsyncScope();
        var systems = scope.ServiceProvider.GetServices<ISimulationSystem>().ToArray();
        var tick = new SimulationTickContext(worldId, new WorldClockSnapshot(worldId, instant, TimeSpan.FromSeconds(1), 1m, instant));
        if (decide) await systems.OfType<NpcUtilityAiSimulationSystem>().Single().ExecuteAsync(tick);
        await systems.OfType<NpcActionExecutionSimulationSystem>().Single().ExecuteAsync(tick);
    }

    private static async Task ConfigureDailyActorAsync(ServiceProvider provider, Guid worldId, Guid npcId, string action, bool valid)
    {
        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<RpgWorldDbContext>();
        var world = await db.Worlds.SingleAsync(value => value.Id == worldId);
        var npc = await db.Actors.OfType<NpcActor>().SingleAsync(value => value.Id == npcId);
        var now = DateTimeOffset.UnixEpoch;
        npc.SetHome(world, null, now);
        if (action == "Eat")
        {
            // Seed hunger at the first tick's world instant.
            npc.AdvanceNeedsTo(now.AddHours(1), 90m, 0m);
            now = now.AddHours(1);
        }
        if (action == "Sleep")
        {
            npc.ConsumeEnergy(90m, now);
            npc.SetHome(world, world.PositionAt(6, 1), now, valid ? null : Guid.NewGuid());
        }
        if (action == "Work")
        {
            npc.AssignJob("farmer", now);
            if (valid)
            {
                var city = City.Create(world, "Farm town", world.PositionAt(6, 1), [world.PositionAt(6, 1)], 1, 100m, now);
                npc.JoinCity(city, now);
                db.Cities.Add(city);
            }
        }
        if (action == "Eat" && valid)
        {
            var tile = await db.Tiles.SingleAsync(value => value.WorldId == worldId && value.X == 6 && value.Y == 1);
            var food = ResourceDeposit.SpawnOnTile(world, tile, new ResourceDefinition("berries", "Berries", "food", 3m), now);
            food.Discover(npc.Id, now);
            db.ResourceDeposits.Add(food);
        }
        if (!valid) npc.SelectAction(action, now);
        await db.SaveChangesAsync();
    }
}

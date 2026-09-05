using Microsoft.Extensions.Logging;
using RpgWorld.Application.Actors;
using RpgWorld.Domain.Actors;
using RpgWorld.Domain.Worlds;
using RpgWorld.Simulation.Actors.Utility;
using RpgWorld.Simulation.Engine;
using RpgWorld.Simulation.Time;

namespace RpgWorld.Simulation.Tests.Actors;

public sealed class NpcUtilityAiSimulationSystemTests
{
    [Fact]
    public async Task System_persists_selected_action_and_records_explainable_diagnostic()
    {
        var start = new DateTimeOffset(2026, 8, 31, 6, 0, 0, TimeSpan.Zero);
        var world = World.Create("Decision system", 8, 8);
        var npc = NpcActor.Create("Hungry villager", world, world.PositionAt(1, 1), start);
        npc.AddInventory("ration", 3, start);
        npc.AdvanceNeedsTo(start.AddHours(10));
        var options = new UtilityAiOptions();
        var repository = new FakeNpcNeedsRepository(npc);
        var diagnostics = new NpcDecisionDiagnostics();
        var logger = new RecordingLogger<NpcUtilityAiSimulationSystem>();
        var system = new NpcUtilityAiSimulationSystem(
            repository,
            new DefaultNpcDecisionContextProvider(options),
            CreateService(options),
            diagnostics,
            logger);
        var instant = start.AddHours(10);
        var context = new SimulationTickContext(
            world.Id,
            new WorldClockSnapshot(world.Id, instant, TimeSpan.FromMinutes(1), 1m, instant));

        await system.ExecuteAsync(context);
        await system.ExecuteAsync(context);

        Assert.Equal(NpcActionCodes.Eat, npc.CurrentAction);
        Assert.Equal(NpcActionCodes.Eat, npc.ActionExecution!.ActionCode);
        Assert.Equal(instant, npc.ActionExecution.StartedAt);
        Assert.Equal(1, repository.SaveCalls);
        var diagnostic = Assert.IsType<NpcDecisionDiagnostic>(diagnostics.GetLatest(npc.Id));
        Assert.Equal(NpcActionCodes.Eat, diagnostic.Decision?.ActionCode);
        Assert.Contains("FoodAvailability", diagnostic.Explanation, StringComparison.Ordinal);
        Assert.Contains(logger.Messages, message =>
            message.Contains("Hunger=", StringComparison.Ordinal) &&
            message.Contains(npc.Id.ToString(), StringComparison.Ordinal));
        Assert.Equal("NpcUtilityAi", system.Name);
        Assert.Equal(30, system.Order);
        Assert.Equal(SimulationSystemFrequencies.NpcDecisions, system.Frequency);
    }

    private static INpcUtilityDecisionService CreateService(UtilityAiOptions options) =>
        new NpcUtilityDecisionService(
            [
                new EatNpcAction(),
                new SleepNpcAction(options),
                new WorkNpcAction(options),
                new TravelNpcAction(options),
                new AttackEnemyNpcAction(options)
            ],
            options);

    private sealed class FakeNpcNeedsRepository(params NpcActor[] npcs) : INpcNeedsRepository
    {
        public int SaveCalls { get; private set; }

        public Task<IReadOnlyList<NpcActor>> ListForUpdateAsync(
            Guid worldId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<NpcActor>>(npcs.Where(npc => npc.WorldId == worldId).ToArray());

        public Task<IReadOnlyList<NpcNeedsSnapshot>> ListUrgentAsync(
            Guid worldId,
            decimal minimumHunger,
            decimal maximumEnergy,
            int limit = 100,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            SaveCalls++;
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingLogger<T> : ILogger<T>
    {
        public List<string> Messages { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            Messages.Add(formatter(state, exception));
    }
}

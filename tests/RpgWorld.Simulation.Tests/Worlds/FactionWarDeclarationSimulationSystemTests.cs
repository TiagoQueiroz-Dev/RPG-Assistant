using RpgWorld.Application.Worlds.Factions;
using RpgWorld.Domain.Events;
using RpgWorld.Domain.Worlds;
using RpgWorld.Domain.Worlds.Factions;
using RpgWorld.Simulation.Engine;
using RpgWorld.Simulation.Time;
using RpgWorld.Simulation.Worlds.Factions;

namespace RpgWorld.Simulation.Tests.Worlds;

public sealed class FactionWarDeclarationSimulationSystemTests
{
    [Fact]
    public async Task Multiple_low_factors_record_score_but_do_not_declare_war()
    {
        var (world, source, target, now) = CreateScenario();
        var repository = new FakeWarRepository(
            [source, target],
            new FactionWarContext(1, 0, 0m, false));
        var options = new WarDeclarationOptions { DeclareWarThreshold = 65m };

        await new FactionWarDeclarationSimulationSystem(repository, new WarScoreCalculator(options))
            .ExecuteAsync(Tick(world, now.AddHours(1)));

        var relation = source.Relations[target.Id];
        Assert.False(relation.LastWarScore!.ReachedThreshold);
        Assert.NotEqual(FactionRelationKind.War, relation.Kind);
        Assert.DoesNotContain(source.DomainEvents, value => value is FactionWarDeclaredEvent);
        Assert.Equal(1, repository.SaveCount);
    }

    [Fact]
    public async Task Multiple_high_factors_cross_configured_threshold_and_declare_war()
    {
        var (world, source, target, now) = CreateScenario();
        source.ApplyRelationModifier(target.Id, new FactionRelationModifier(
            FactionRelationModifierSource.History, "Ancient feud.", affinityDelta: -50, tensionDelta: 50),
            now.AddMinutes(1));
        source.ClearDomainEvents();
        var repository = new FakeWarRepository(
            [source, target],
            new FactionWarContext(5, 3, 500m, true));
        var options = new WarDeclarationOptions { DeclareWarThreshold = 60m };

        await new FactionWarDeclarationSimulationSystem(repository, new WarScoreCalculator(options))
            .ExecuteAsync(Tick(world, now.AddHours(1)));

        var relation = source.Relations[target.Id];
        Assert.True(relation.LastWarScore!.ReachedThreshold);
        Assert.Equal(FactionRelationKind.War, relation.Kind);
        var declared = Assert.Single(source.DomainEvents.OfType<FactionWarDeclaredEvent>());
        Assert.Equal(source.Id, declared.FactionId);
        Assert.Equal(target.Id, declared.TargetFactionId);
        Assert.False(declared.ForcedByGameMaster);
    }

    [Fact]
    public void Threshold_is_configurable_for_the_same_score()
    {
        var (_, source, target, now) = CreateScenario();
        var context = new FactionWarContext(5, 3, 500m, true);
        var lowThreshold = new WarScoreCalculator(new WarDeclarationOptions { DeclareWarThreshold = 50m })
            .Calculate(source, target, context, now);
        var highThreshold = new WarScoreCalculator(new WarDeclarationOptions { DeclareWarThreshold = 100m })
            .Calculate(source, target, context, now);

        Assert.True(lowThreshold.ReachedThreshold);
        Assert.False(highThreshold.ReachedThreshold);
        Assert.Equal(lowThreshold.Total, highThreshold.Total);
    }

    private static (World World, Faction Source, Faction Target, DateTimeOffset Now) CreateScenario()
    {
        var now = DateTimeOffset.UnixEpoch;
        var world = World.Create("War world", 8, 8);
        var source = Faction.Create(world, "North", FactionType.Kingdom, Guid.NewGuid(), 0m, 100m, now);
        var target = Faction.Create(world, "South", FactionType.Kingdom, Guid.NewGuid(), 0m, 20m, now);
        source.ClearDomainEvents();
        target.ClearDomainEvents();
        return (world, source, target, now);
    }

    private static SimulationTickContext Tick(World world, DateTimeOffset instant) => new(
        world.Id, new WorldClockSnapshot(world.Id, instant, TimeSpan.FromMinutes(30), 1m, instant));

    private sealed class FakeWarRepository(
        IReadOnlyList<Faction> factions,
        FactionWarContext context) : IFactionWarRepository
    {
        public int SaveCount { get; private set; }

        public Task<IReadOnlyList<Faction>> ListActiveAsync(Guid worldId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Faction>>(factions.Where(faction => faction.WorldId == worldId).ToArray());

        public Task<FactionWarContext> BuildContextAsync(Faction source, Faction target, CancellationToken cancellationToken = default) =>
            Task.FromResult(context);

        public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            SaveCount++;
            return Task.CompletedTask;
        }
    }
}

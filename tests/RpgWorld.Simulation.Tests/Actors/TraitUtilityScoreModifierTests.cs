using RpgWorld.Domain.Actors;
using RpgWorld.Domain.Actors.Traits;
using RpgWorld.Domain.Worlds;
using RpgWorld.Simulation.Actors.Utility;

namespace RpgWorld.Simulation.Tests.Actors;

public sealed class TraitUtilityScoreModifierTests
{
    [Fact]
    public void Trait_can_change_the_selected_action_and_is_explained()
    {
        var now = DateTimeOffset.UnixEpoch;
        var world = World.Create("Trait decisions", 8, 8);
        var npc = NpcActor.Create("Mercenary", world, world.PositionAt(1, 1), now);
        npc.AssignJob("guard", now);
        var aggressive = Trait(
            "aggressive",
            new Dictionary<string, decimal> { [NpcActionCodes.AttackEnemy] = 1.5m });
        var catalog = new TraitDefinitionCatalog([aggressive]);
        var service = CreateService(catalog);
        var context = new NpcDecisionContext(npc, 0m, 0.5m, 0m, true, 0.7m);

        var before = Assert.IsType<NpcDecision>(service.Decide(context));
        npc.AddTrait(aggressive, now);
        var after = Assert.IsType<NpcDecision>(service.Decide(context));

        Assert.Equal(NpcActionCodes.Work, before.ActionCode);
        Assert.Equal(NpcActionCodes.AttackEnemy, after.ActionCode);
        Assert.Contains("Trait:aggressive x1.5", after.Explain(), StringComparison.Ordinal);
    }

    [Fact]
    public void Multiple_traits_compose_predictably_on_the_same_action()
    {
        var now = DateTimeOffset.UnixEpoch;
        var world = World.Create("Combined traits", 8, 8);
        var npc = NpcActor.Create("Defender", world, world.PositionAt(1, 1), now);
        var brave = Trait("brave", new Dictionary<string, decimal> { [NpcActionCodes.AttackEnemy] = 1.2m });
        var loyal = Trait("loyal", new Dictionary<string, decimal> { [NpcActionCodes.AttackEnemy] = 1.1m });
        npc.AddTrait(brave, now);
        npc.AddTrait(loyal, now);
        var service = CreateService(new TraitDefinitionCatalog([brave, loyal]));
        var decision = Assert.IsType<NpcDecision>(service.Decide(
            new NpcDecisionContext(npc, 0m, 1m, 0m, true, 0.4m)));
        var attack = decision.Candidates.Single(candidate => candidate.ActionCode == NpcActionCodes.AttackEnemy);

        Assert.Equal(0.4m, attack.BaseScore);
        Assert.Equal(0.528m, attack.Score);
        Assert.Equal(["Trait:brave", "Trait:loyal"], attack.Modifiers.Select(modifier => modifier.Source));
    }

    private static INpcUtilityDecisionService CreateService(ITraitDefinitionCatalog catalog)
    {
        var options = new UtilityAiOptions();
        return new NpcUtilityDecisionService(
            [
                new EatNpcAction(),
                new SleepNpcAction(options),
                new WorkNpcAction(options),
                new TravelNpcAction(options),
                new AttackEnemyNpcAction(options)
            ],
            options,
            [new TraitUtilityScoreModifier(catalog)]);
    }

    private static TraitDefinition Trait(string code, IReadOnlyDictionary<string, decimal> modifiers) =>
        new(code, code, $"{code} test trait.", modifiers);
}

using RpgWorld.Domain.Actors;
using RpgWorld.Domain.Worlds;
using RpgWorld.Simulation.Actors.Utility;

namespace RpgWorld.Simulation.Tests.Actors;

public sealed class RelationshipUtilityScoreModifierTests
{
    [Fact]
    public void Hatred_can_change_decision_while_fear_reduces_attack_utility()
    {
        var now = DateTimeOffset.UnixEpoch;
        var world = World.Create("Relationship decisions", 8, 8);
        var npc = NpcActor.Create("Guard", world, world.PositionAt(1, 1), now);
        var targetId = Guid.NewGuid();
        npc.AssignJob("guard", now);
        var service = CreateService();
        var context = new NpcDecisionContext(npc, 0m, 0.5m, 0m, true, 0.7m);

        var before = Assert.IsType<NpcDecision>(service.Decide(context));
        npc.ApplyRelationship(targetId, new ActorRelationshipModifier("betrayal", hatred: 100), now);
        var hatredDecision = Assert.IsType<NpcDecision>(service.Decide(context));
        npc.ApplyRelationship(targetId, new ActorRelationshipModifier("intimidation", fear: 100), now);
        var fearedAttack = Assert.IsType<NpcDecision>(service.Decide(context)).Candidates
            .Single(candidate => candidate.ActionCode == NpcActionCodes.AttackEnemy);

        Assert.Equal(NpcActionCodes.Work, before.ActionCode);
        Assert.Equal(NpcActionCodes.AttackEnemy, hatredDecision.ActionCode);
        Assert.True(fearedAttack.Score < hatredDecision.Score);
        Assert.Contains("hatred=100", hatredDecision.Explain(), StringComparison.Ordinal);
    }

    private static INpcUtilityDecisionService CreateService()
    {
        var options = new UtilityAiOptions();
        return new NpcUtilityDecisionService(
            [new EatNpcAction(), new SleepNpcAction(options), new WorkNpcAction(options), new TravelNpcAction(options), new AttackEnemyNpcAction(options)],
            options,
            [new RelationshipUtilityScoreModifier()]);
    }
}

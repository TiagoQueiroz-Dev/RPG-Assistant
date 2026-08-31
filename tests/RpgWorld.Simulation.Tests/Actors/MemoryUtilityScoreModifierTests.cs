using RpgWorld.Domain.Actors;
using RpgWorld.Domain.Actors.Memories;
using RpgWorld.Domain.Worlds;
using RpgWorld.Simulation.Actors.Utility;

namespace RpgWorld.Simulation.Tests.Actors;

public sealed class MemoryUtilityScoreModifierTests
{
    [Fact]
    public void Important_hostile_memory_changes_decision_from_work_to_attack()
    {
        var now = DateTimeOffset.UnixEpoch;
        var world = World.Create("Remembered hostility", 8, 8);
        var npc = NpcActor.Create("Guard", world, world.PositionAt(1, 1), now);
        npc.AssignJob("guard", now);
        var memory = NpcMemory.Create(
            npc.Id,
            world.Id,
            NpcMemoryEventTypes.FamilyMemberKilled,
            Guid.NewGuid(),
            80,
            now);
        var service = CreateService();

        var before = Assert.IsType<NpcDecision>(service.Decide(
            new NpcDecisionContext(npc, 0m, 0.5m, 0m, true, 0.7m)));
        var after = Assert.IsType<NpcDecision>(service.Decide(
            new NpcDecisionContext(npc, 0m, 0.5m, 0m, true, 0.7m, [memory])));

        Assert.Equal(NpcActionCodes.Work, before.ActionCode);
        Assert.Equal(NpcActionCodes.AttackEnemy, after.ActionCode);
        Assert.Contains("Importance 80/100", after.Explain(), StringComparison.Ordinal);
    }

    [Fact]
    public void Higher_importance_produces_a_stronger_memory_modifier()
    {
        var now = DateTimeOffset.UnixEpoch;
        var world = World.Create("Importance", 8, 8);
        var npc = NpcActor.Create("NPC", world, world.PositionAt(1, 1), now);
        var low = NpcMemory.Create(npc.Id, world.Id, NpcMemoryEventTypes.WasAttacked, Guid.NewGuid(), 20, now);
        var high = NpcMemory.Create(npc.Id, world.Id, NpcMemoryEventTypes.WasAttacked, Guid.NewGuid(), 80, now);
        var modifier = new MemoryUtilityScoreModifier();
        var action = new AttackEnemyNpcAction(new UtilityAiOptions());

        var lowEffect = Assert.Single(modifier.GetModifiers(
            action, new NpcDecisionContext(npc, 0m, 0.5m, 0m, true, 0.5m, [low])));
        var highEffect = Assert.Single(modifier.GetModifiers(
            action, new NpcDecisionContext(npc, 0m, 0.5m, 0m, true, 0.5m, [high])));

        Assert.True(highEffect.Multiplier > lowEffect.Multiplier);
    }

    private static INpcUtilityDecisionService CreateService()
    {
        var options = new UtilityAiOptions();
        return new NpcUtilityDecisionService(
            [new EatNpcAction(), new SleepNpcAction(options), new WorkNpcAction(options), new TravelNpcAction(options), new AttackEnemyNpcAction(options)],
            options,
            [new MemoryUtilityScoreModifier()]);
    }
}

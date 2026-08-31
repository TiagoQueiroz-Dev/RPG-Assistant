using RpgWorld.Domain.Actors;
using RpgWorld.Domain.Worlds;

namespace RpgWorld.Domain.Tests.Actors;

public sealed class NpcNeedsTests
{
    [Fact]
    public void Needs_evolve_over_several_world_hours_and_remain_normalized()
    {
        var start = new DateTimeOffset(2026, 8, 31, 6, 0, 0, TimeSpan.Zero);
        var world = World.Create("Needs", 8, 8);
        var npc = NpcActor.Create("Farmer", world, world.PositionAt(1, 1), start);

        npc.AdvanceNeedsTo(start.AddHours(6));

        Assert.Equal(24m, npc.Hunger);
        Assert.Equal(82m, npc.Energy);
        Assert.Equal(start.AddHours(6), npc.NeedsUpdatedAt);

        npc.AdvanceNeedsTo(start.AddHours(36));

        Assert.Equal(100m, npc.Hunger);
        Assert.Equal(0m, npc.Energy);
    }

    [Fact]
    public void Daily_actions_catch_up_time_and_restore_or_consume_resources()
    {
        var start = DateTimeOffset.UnixEpoch;
        var actionTime = start.AddHours(5);
        var world = World.Create("Daily life", 8, 8);
        var npc = NpcActor.Create("Smith", world, world.PositionAt(2, 2), start);
        var familyMemberId = Guid.NewGuid();
        var factionId = Guid.NewGuid();

        npc.Eat(10m, actionTime);
        npc.Rest(20m, actionTime);
        npc.ConsumeEnergy(30m, actionTime);
        npc.Earn(50m, actionTime);
        npc.Spend(12.5m, actionTime);
        npc.AssignJob("  blacksmith  ", actionTime);
        npc.SetHome(world, world.PositionAt(3, 4), actionTime);
        npc.AddFamilyMember(familyMemberId, actionTime);
        npc.AddFamilyMember(familyMemberId, actionTime);
        npc.JoinFaction(factionId, actionTime);
        npc.SetGoal("provide-for-family", 80, familyMemberId, actionTime);
        npc.SetGoal("rest", 20, null, actionTime);

        Assert.Equal(10m, npc.Hunger);
        Assert.Equal(70m, npc.Energy);
        Assert.Equal(37.5m, npc.Money);
        Assert.Equal("blacksmith", npc.Job);
        Assert.Equal(world.PositionAt(3, 4), npc.Home);
        Assert.Equal(familyMemberId, Assert.Single(npc.FamilyIds));
        Assert.Equal(factionId, npc.FactionId);
        Assert.Equal(["provide-for-family", "rest"], npc.Goals.Select(goal => goal.Code));
    }

    [Fact]
    public void Invalid_need_values_and_actions_are_rejected_without_corrupting_state()
    {
        var start = DateTimeOffset.UnixEpoch;
        var world = World.Create("Invariants", 4, 4);
        var npc = NpcActor.Create("Guard", world, world.PositionAt(0, 0), start);

        Assert.Throws<ArgumentOutOfRangeException>(() => npc.AdvanceNeedsTo(start.AddHours(1), -1m, 1m));
        Assert.Throws<ArgumentOutOfRangeException>(() => npc.AdvanceNeedsTo(start.AddHours(1), 1m, -1m));
        Assert.Throws<ArgumentOutOfRangeException>(() => npc.AdvanceNeedsTo(start.AddMinutes(-1)));
        Assert.Throws<ArgumentOutOfRangeException>(() => npc.Eat(0m, start));
        Assert.Throws<ArgumentOutOfRangeException>(() => npc.Rest(-1m, start));
        Assert.Throws<ArgumentOutOfRangeException>(() => npc.ConsumeEnergy(0m, start));
        Assert.Throws<ArgumentOutOfRangeException>(() => npc.Earn(-1m, start));
        Assert.Throws<ArgumentOutOfRangeException>(() => npc.Spend(1m, start));
        Assert.Throws<ArgumentException>(() => npc.AddFamilyMember(npc.Id, start));
        Assert.Throws<ArgumentOutOfRangeException>(() => npc.SetGoal("invalid", 101, null, start));

        Assert.Equal(0m, npc.Hunger);
        Assert.Equal(100m, npc.Energy);
        Assert.Equal(0m, npc.Money);
        Assert.Equal(start, npc.NeedsUpdatedAt);
    }
}

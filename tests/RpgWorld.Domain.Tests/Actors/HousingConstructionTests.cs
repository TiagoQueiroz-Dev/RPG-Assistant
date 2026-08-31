using RpgWorld.Domain.Actors;
using RpgWorld.Domain.Actors.Housing;
using RpgWorld.Domain.Worlds;

namespace RpgWorld.Domain.Tests.Actors;

public sealed class HousingConstructionTests
{
    [Fact]
    public void Construction_consumes_resources_in_two_stages_and_completes()
    {
        var now = DateTimeOffset.UnixEpoch;
        var world = World.Create("Housing", 8, 8);
        var npc = NpcActor.Create("Builder", world, world.PositionAt(1, 1), now);
        var familyMemberId = Guid.NewGuid();
        npc.AddFamilyMember(familyMemberId, now);
        npc.AddInventory("wood", 4, now);
        npc.AddInventory("stone", 2, now);
        var construction = HousingConstruction.Create(npc, world.PositionAt(2, 2), 4, 2, now);

        Assert.Equal([npc.Id, familyMemberId], construction.ResidentActorIds);

        construction.Advance(npc, now.AddHours(1));
        Assert.Equal(50, construction.Progress);
        Assert.Equal(2, npc.InventoryQuantity("wood"));
        Assert.Equal(1, npc.InventoryQuantity("stone"));

        construction.Advance(npc, now.AddHours(2));
        Assert.Equal(HousingConstructionStatus.Completed, construction.Status);
        Assert.Equal(100, construction.Progress);
        Assert.Equal(0, npc.InventoryQuantity("wood"));
        Assert.Equal(0, npc.InventoryQuantity("stone"));
    }

    [Fact]
    public void Construction_does_not_advance_without_resources()
    {
        var world = World.Create("No resources", 8, 8);
        var npc = NpcActor.Create("Builder", world, world.PositionAt(1, 1), DateTimeOffset.UnixEpoch);
        var construction = HousingConstruction.Create(npc, world.PositionAt(2, 2), 4, 2, DateTimeOffset.UnixEpoch);

        Assert.False(construction.CanAdvance(npc));
        Assert.Throws<InvalidOperationException>(() => construction.Advance(npc, DateTimeOffset.UnixEpoch));
        Assert.Equal(0, construction.Progress);
    }

    [Fact]
    public void Single_unit_cost_still_uses_two_explicit_construction_stages()
    {
        var now = DateTimeOffset.UnixEpoch;
        var world = World.Create("Small house", 8, 8);
        var npc = NpcActor.Create("Builder", world, world.PositionAt(1, 1), now);
        npc.AddInventory("wood", 1, now);
        npc.AddInventory("stone", 1, now);
        var construction = HousingConstruction.Create(npc, world.PositionAt(2, 2), 1, 1, now);

        construction.Advance(npc, now.AddHours(1));
        Assert.Equal(50, construction.Progress);
        Assert.Equal(HousingConstructionStatus.InProgress, construction.Status);

        construction.Advance(npc, now.AddHours(2));
        Assert.Equal(100, construction.Progress);
        Assert.Equal(HousingConstructionStatus.Completed, construction.Status);
    }
}

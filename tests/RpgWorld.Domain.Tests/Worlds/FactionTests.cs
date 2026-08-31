using RpgWorld.Domain.Events;
using RpgWorld.Domain.Worlds;
using RpgWorld.Domain.Worlds.Factions;

namespace RpgWorld.Domain.Tests.Worlds;

public sealed class FactionTests
{
    [Fact]
    public void Creates_every_supported_type_with_leader_and_creation_event()
    {
        var world = World.Create("Political world", 8, 8);
        var leaderId = Guid.NewGuid();

        foreach (var type in Enum.GetValues<FactionType>())
        {
            var faction = Faction.Create(world, $"Faction {type}", type, leaderId, 100m, 25m, DateTimeOffset.UnixEpoch);

            Assert.Equal(type, faction.Type);
            Assert.Equal(leaderId, faction.LeaderActorId);
            Assert.Equal([leaderId], faction.MemberActorIds);
            Assert.Equal(FactionHistoryEventTypes.Created, Assert.Single(faction.History).EventType);
            Assert.IsType<FactionCreatedEvent>(Assert.Single(faction.DomainEvents));
        }
    }

    [Fact]
    public void Members_join_leave_and_leadership_changes_without_corrupting_membership()
    {
        var now = DateTimeOffset.UnixEpoch;
        var world = World.Create("Leadership", 8, 8);
        var originalLeader = Guid.NewGuid();
        var successor = Guid.NewGuid();
        var faction = Faction.Create(world, "Crown", FactionType.Kingdom, originalLeader, 0m, 0m, now);
        faction.ClearDomainEvents();

        Assert.True(faction.AddMember(successor, now.AddHours(1)));
        Assert.Throws<InvalidOperationException>(() =>
            faction.RemoveMember(originalLeader, "Abdicated too early", now.AddHours(2)));
        faction.ChangeLeader(successor, "The council elected a successor.", now.AddHours(2));
        Assert.True(faction.RemoveMember(originalLeader, "The former ruler retired.", now.AddHours(3)));

        Assert.Equal(successor, faction.LeaderActorId);
        Assert.Equal([successor], faction.MemberActorIds);
        Assert.Collection(
            faction.DomainEvents,
            value => Assert.IsType<FactionMemberJoinedEvent>(value),
            value => Assert.IsType<FactionLeaderChangedEvent>(value),
            value => Assert.IsType<FactionMemberLeftEvent>(value));
        Assert.Equal(
            [FactionHistoryEventTypes.Created, FactionHistoryEventTypes.MemberJoined,
                FactionHistoryEventTypes.LeaderChanged, FactionHistoryEventTypes.MemberLeft],
            faction.History.Select(entry => entry.EventType));
    }

    [Fact]
    public void Territory_power_wealth_cities_and_relations_are_queryable()
    {
        var now = DateTimeOffset.UnixEpoch;
        var world = World.Create("Influence", 8, 8);
        var faction = Faction.Create(world, "Guild", FactionType.MerchantGuild, Guid.NewGuid(), 50m, 10m, now);
        var territory = new[] { world.PositionAt(1, 1), world.PositionAt(5, 6) };
        var cityId = Guid.NewGuid();
        var targetFactionId = Guid.NewGuid();

        Assert.Equal(2, faction.ClaimTerritory(world, territory, now.AddHours(1)));
        Assert.True(faction.AssociateCity(cityId, now.AddHours(2)));
        faction.AdjustWealth(25m, "Successful trade season.", now.AddHours(3));
        faction.SetMilitaryPower(35m, "Hired guards.", now.AddHours(4));
        faction.ApplyRelationModifier(
            targetFactionId,
            new FactionRelationModifier(
                FactionRelationModifierSource.Trade, "Trade alliance.", affinityDelta: 60),
            now.AddHours(5));

        Assert.Equal(territory.ToHashSet(), faction.Territory.ToHashSet());
        Assert.Equal(cityId, Assert.Single(faction.ControlledCityIds));
        Assert.Equal(75m, faction.Wealth);
        Assert.Equal(35m, faction.MilitaryPower);
        var relation = faction.Relations[targetFactionId];
        Assert.Equal(FactionRelationKind.Alliance, relation.Kind);
        Assert.Equal(60, relation.Affinity);
    }

    [Fact]
    public void Dissolution_releases_state_raises_event_and_preserves_history()
    {
        var now = DateTimeOffset.UnixEpoch;
        var world = World.Create("Dissolution", 8, 8);
        var leaderId = Guid.NewGuid();
        var faction = Faction.Create(world, "Last Army", FactionType.Army, leaderId, 10m, 20m, now);
        faction.ClaimTerritory(world, [world.PositionAt(0, 0)], now.AddHours(1));
        faction.AssociateCity(Guid.NewGuid(), now.AddHours(2));
        faction.ClearDomainEvents();

        faction.Dissolve("The army disbanded.", now.AddHours(3));

        Assert.Equal(FactionStatus.Dissolved, faction.Status);
        Assert.Null(faction.LeaderActorId);
        Assert.Empty(faction.MemberActorIds);
        Assert.Empty(faction.ControlledCityIds);
        Assert.Empty(faction.Territory);
        Assert.All(faction.TerritoryTiles, tile =>
        {
            Assert.False(tile.IsActive);
            Assert.Equal(now.AddHours(3), tile.ReleasedAtUtc);
        });
        Assert.Equal(FactionHistoryEventTypes.Dissolved, faction.History[^1].EventType);
        Assert.Equal(leaderId.ToString(), faction.History[^1].Metadata["formerLeaderActorId"]);
        Assert.IsType<FactionDissolvedEvent>(Assert.Single(faction.DomainEvents));
        Assert.Throws<InvalidOperationException>(() => faction.AdjustWealth(1m, "Impossible", now.AddHours(4)));
    }
}

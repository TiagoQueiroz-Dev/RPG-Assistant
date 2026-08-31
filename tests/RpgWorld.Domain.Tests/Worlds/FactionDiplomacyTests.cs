using RpgWorld.Domain.Events;
using RpgWorld.Domain.Worlds;
using RpgWorld.Domain.Worlds.Factions;

namespace RpgWorld.Domain.Tests.Worlds;

public sealed class FactionDiplomacyTests
{
    [Fact]
    public void Relations_are_directional_and_independent()
    {
        var now = DateTimeOffset.UnixEpoch;
        var world = World.Create("Directed diplomacy", 8, 8);
        var first = CreateFaction(world, "First", now);
        var second = CreateFaction(world, "Second", now);

        first.ApplyRelationModifier(
            second.Id,
            new FactionRelationModifier(
                FactionRelationModifierSource.Trade, "Opened trade route.", affinityDelta: 25, tensionDelta: -10),
            now.AddHours(1));

        Assert.Equal(25, first.Relations[second.Id].Affinity);
        Assert.Empty(second.Relations);
    }

    [Fact]
    public void Event_modifiers_transition_neutral_to_hostile_then_hostile_to_war()
    {
        var now = DateTimeOffset.UnixEpoch;
        var world = World.Create("Escalating diplomacy", 8, 8);
        var faction = CreateFaction(world, "North", now);
        var target = CreateFaction(world, "South", now);
        var borderIncidentId = Guid.NewGuid();
        faction.ClearDomainEvents();

        var hostile = faction.ApplyRelationModifier(
            target.Id,
            new FactionRelationModifier(
                FactionRelationModifierSource.Event,
                "Border patrol attacked.",
                affinityDelta: -35,
                tensionDelta: 55,
                sourceEventId: borderIncidentId),
            now.AddHours(1));

        Assert.Equal(FactionRelationKind.Hostile, hostile.Kind);
        Assert.Equal(-35, hostile.Affinity);
        Assert.Equal(55, hostile.Tension);
        var hostileEvent = Assert.IsType<FactionDiplomaticStateChangedEvent>(Assert.Single(faction.DomainEvents));
        Assert.Equal(FactionRelationKind.Neutral, hostileEvent.PreviousState);
        Assert.Equal(FactionRelationKind.Hostile, hostileEvent.State);
        Assert.Equal(borderIncidentId, hostileEvent.SourceWorldEventId);

        faction.ClearDomainEvents();
        var war = faction.ApplyRelationModifier(
            target.Id,
            new FactionRelationModifier(
                FactionRelationModifierSource.Border,
                "Troops crossed the frontier.",
                affinityDelta: -20,
                tensionDelta: 30),
            now.AddHours(2));

        Assert.Equal(FactionRelationKind.War, war.Kind);
        Assert.Equal(85, war.Tension);
        var warEvent = Assert.IsType<FactionDiplomaticStateChangedEvent>(Assert.Single(faction.DomainEvents));
        Assert.Equal(FactionRelationKind.Hostile, warEvent.PreviousState);
        Assert.Equal(FactionRelationKind.War, warEvent.State);
        Assert.Equal(2, war.History.Count);
        Assert.Equal(
            [FactionHistoryEventTypes.DiplomaticStateChanged, FactionHistoryEventTypes.DiplomaticStateChanged],
            faction.History.TakeLast(2).Select(entry => entry.EventType));
    }

    [Theory]
    [InlineData(70, 10, FactionRelationKind.Alliance)]
    [InlineData(10, 20, FactionRelationKind.Neutral)]
    [InlineData(-30, 20, FactionRelationKind.Hostile)]
    [InlineData(20, 50, FactionRelationKind.Hostile)]
    [InlineData(-80, 10, FactionRelationKind.War)]
    [InlineData(20, 80, FactionRelationKind.War)]
    public void State_is_mapped_from_affinity_and_tension(
        int affinity,
        int tension,
        FactionRelationKind expected) =>
        Assert.Equal(expected, FactionRelation.ResolveState(affinity, tension));

    [Fact]
    public void Vassalage_is_explicit_and_can_end_back_in_mapped_state()
    {
        var now = DateTimeOffset.UnixEpoch;
        var target = Guid.NewGuid();
        var relation = FactionRelation.Neutral(target, now)
            .Apply(new FactionRelationModifier(
                FactionRelationModifierSource.Leadership, "Accepted a liege.", vassalage: true), now.AddHours(1));

        Assert.Equal(FactionRelationKind.Vassal, relation.Kind);
        Assert.True(relation.IsVassal);

        relation = relation.Apply(new FactionRelationModifier(
            FactionRelationModifierSource.History, "Vassalage ended.", vassalage: false), now.AddHours(2));

        Assert.Equal(FactionRelationKind.Neutral, relation.Kind);
        Assert.False(relation.IsVassal);
    }

    [Fact]
    public void Game_master_can_prevent_autonomous_war_and_force_it_when_required()
    {
        var now = DateTimeOffset.UnixEpoch;
        var world = World.Create("Controlled war", 8, 8);
        var faction = CreateFaction(world, "First", now);
        var target = CreateFaction(world, "Second", now);
        var score = new FactionWarScore(
            new FactionWarFactors(100m, 100m, 100m, 100m, 100m), 100m, 65m, now.AddHours(1));
        faction.ClearDomainEvents();

        faction.PreventWar(target.Id, now.AddDays(1), "Campaign preparation.", now.AddHours(1));
        Assert.False(faction.DeclareWar(target.Id, score, "Autonomous escalation.", false, now.AddHours(2)));
        Assert.NotEqual(FactionRelationKind.War, faction.Relations[target.Id].Kind);

        Assert.True(faction.DeclareWar(
            target.Id, score with { EvaluatedAtUtc = now.AddHours(2) }, "The GM starts the war.", true, now.AddHours(2)));
        Assert.Equal(FactionRelationKind.War, faction.Relations[target.Id].Kind);
        var warEvent = Assert.Single(faction.DomainEvents.OfType<FactionWarDeclaredEvent>());
        Assert.True(warEvent.ForcedByGameMaster);
        Assert.Equal(100m, warEvent.WarScore.Total);
        Assert.Equal(FactionHistoryEventTypes.WarDeclared, faction.History[^1].EventType);
    }

    private static Faction CreateFaction(World world, string name, DateTimeOffset now) =>
        Faction.Create(world, name, FactionType.Kingdom, Guid.NewGuid(), 0m, 0m, now);
}

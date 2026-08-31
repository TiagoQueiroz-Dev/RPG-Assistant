using RpgWorld.Domain.Actors;
using RpgWorld.Domain.Actors.Traits;
using RpgWorld.Domain.Worlds;
using RpgWorld.Modules.Abstractions.Actors;
using RpgWorld.Modules.Default.Actors;

namespace RpgWorld.Domain.Tests.Actors;

public sealed class TraitTests
{
    [Theory]
    [InlineData("brave", "AttackEnemy", 1.35)]
    [InlineData("coward", "AttackEnemy", 0.55)]
    [InlineData("greedy", "Work", 1.40)]
    [InlineData("loyal", "AttackEnemy", 1.15)]
    [InlineData("aggressive", "AttackEnemy", 1.50)]
    [InlineData("peaceful", "AttackEnemy", 0.35)]
    [InlineData("curious", "Travel", 1.45)]
    [InlineData("religious", "Sleep", 1.10)]
    [InlineData("ambitious", "Work", 1.30)]
    public void Default_traits_have_testable_action_effects(
        string traitCode,
        string actionCode,
        double expectedMultiplier)
    {
        var trait = DefaultActorDefinitions.Catalog.Resolve(traitCode);

        Assert.True(trait.TryGetMultiplier(actionCode, out var multiplier));
        Assert.Equal((decimal)expectedMultiplier, multiplier);
        Assert.False(string.IsNullOrWhiteSpace(trait.Description));
    }

    [Fact]
    public void Npc_can_combine_and_remove_catalog_traits_without_duplicates()
    {
        var world = World.Create("Traits", 8, 8);
        var now = DateTimeOffset.UnixEpoch;
        var npc = NpcActor.Create("Distinct", world, world.PositionAt(1, 1), now);
        var brave = DefaultActorDefinitions.Catalog.Resolve("brave");
        var curious = DefaultActorDefinitions.Catalog.Resolve("curious");

        npc.AddTrait(brave, now);
        npc.AddTrait(curious, now);
        npc.AddTrait(brave, now);

        Assert.Equal(["brave", "curious"], npc.TraitCodes);

        npc.RemoveTrait("BRAVE", now);

        Assert.Equal(["curious"], npc.TraitCodes);
    }

    [Fact]
    public void External_actor_modules_are_merged_into_the_trait_catalog()
    {
        var custom = new TraitDefinition(
            "scholarly",
            "Scholarly",
            "Studies the world methodically.",
            new Dictionary<string, decimal> { ["Travel"] = 1.2m });
        var catalog = TraitDefinitionCatalogFactory.Create([new TestActorModule(custom)]);

        Assert.Same(custom, catalog.Resolve("SCHOLARLY"));
        Assert.Throws<ArgumentException>(() =>
            TraitDefinitionCatalogFactory.Create([
                new TestActorModule(custom),
                new TestActorModule(custom)
            ]));
    }

    [Fact]
    public void Trait_definition_rejects_invalid_modifiers()
    {
        Assert.Throws<ArgumentException>(() => new TraitDefinition(
            "empty",
            "Empty",
            "No effect.",
            new Dictionary<string, decimal>()));
        Assert.Throws<ArgumentOutOfRangeException>(() => new TraitDefinition(
            "invalid",
            "Invalid",
            "Invalid multiplier.",
            new Dictionary<string, decimal> { ["Work"] = 0m }));
    }

    private sealed class TestActorModule(params TraitDefinition[] traits) : IActorDefinitionModule
    {
        public IEnumerable<TraitDefinition> Traits => traits;
    }
}

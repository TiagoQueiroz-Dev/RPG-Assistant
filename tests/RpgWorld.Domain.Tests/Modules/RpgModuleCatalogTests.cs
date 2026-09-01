using RpgWorld.Domain.Actors.Traits;
using RpgWorld.Domain.Worlds.Definitions;
using RpgWorld.Modules.Abstractions;
using RpgWorld.Modules.Abstractions.Definitions;
using RpgWorld.Modules.Default;

namespace RpgWorld.Domain.Tests.Modules;

public sealed class RpgModuleCatalogTests
{
    [Fact]
    public void Discovers_default_module_and_builds_unified_engine_catalog()
    {
        var modules = RpgModuleCatalog.Discover(
            RpgModuleCatalog.CurrentEngineVersion,
            typeof(DefaultRpgModule).Assembly);

        var content = modules.ForCampaign(new CampaignModuleSelection(
            Guid.NewGuid(), "rpgworld.default"));

        Assert.Equal("rpgworld.default", Assert.Single(modules.AvailableModules).Id);
        Assert.Equal("wolf", content.ResolveCreature("wolf").Code);
        Assert.Equal("human-commoner", content.ResolveNpc("human-commoner").Code);
        Assert.Equal("travel-ration", content.ResolveItem("travel-ration").Code);
        Assert.Equal("forest", content.ResolveBiome("forest").Code);
        Assert.Equal("movement", content.ResolveRule("movement").Code);
        Assert.Equal(4m, content.ResolveRule("survival").Parameters["hunger-per-hour"]);
        Assert.Contains(content.Resources, value => value.Code == "food");
        Assert.Equal("brave", content.Resolve("brave").Code);
        Assert.IsAssignableFrom<IWorldDefinitionCatalog>(content);
        Assert.IsAssignableFrom<ITraitDefinitionCatalog>(content);
    }

    [Fact]
    public void Campaign_enables_only_selected_example_module_content()
    {
        var modules = new RpgModuleCatalog([new DefaultRpgModule(), new ExampleRpgModule()]);

        var content = modules.ForCampaign(new CampaignModuleSelection(Guid.NewGuid(), "example.module"));

        Assert.Equal("example.module", Assert.Single(content.Modules).Id);
        Assert.Equal("crystal-golem", content.ResolveCreature("crystal-golem").Code);
        Assert.Equal("crystal-shard", content.ResolveItem("crystal-shard").Code);
        Assert.Equal("crystal-cavern", content.ResolveBiome("crystal-cavern").Code);
        Assert.Equal("resonance", content.ResolveRule("resonance").Code);
        Assert.Throws<KeyNotFoundException>(() => content.ResolveCreature("wolf"));
    }

    [Fact]
    public void Rejects_module_incompatible_with_engine()
    {
        var incompatible = new ExampleRpgModule(new RpgModuleMetadata(
            "future.module", "Future", new Version(2, 0), new Version(2, 0)));

        var exception = Assert.Throws<InvalidOperationException>(() =>
            new RpgModuleCatalog([incompatible], new Version(1, 0)));

        Assert.Contains("incompatible", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class ExampleRpgModule : IRpgModule
    {
        public ExampleRpgModule() : this(new RpgModuleMetadata(
            "example.module", "Example", new Version(1, 0), new Version(1, 0))) { }

        public ExampleRpgModule(RpgModuleMetadata metadata) => Metadata = metadata;

        public RpgModuleMetadata Metadata { get; }
        public IEnumerable<TerrainDefinition> Terrains =>
            [new("crystal", "Crystal", 1.2m, true, false)];
        public IEnumerable<BiomeDefinition> Biomes =>
            [new("crystal-cavern", "Crystal Cavern", "crystal", -10m, 30m, 0.1m, 0.8m)];
        public IEnumerable<TraitDefinition> Traits => [];
        public IEnumerable<CreatureDefinition> Creatures =>
            [new("crystal-golem", "Crystal Golem", 180, ["construct"])];
        public IEnumerable<ItemDefinition> Items =>
            [new("crystal-shard", "Crystal Shard", "material", true)];
        public IEnumerable<RuleDefinition> Rules =>
            [new("resonance", "Resonance", new Dictionary<string, decimal> { ["power"] = 1.5m })];
    }
}

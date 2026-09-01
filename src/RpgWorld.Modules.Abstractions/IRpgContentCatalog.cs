using RpgWorld.Domain.Actors.Traits;
using RpgWorld.Domain.Worlds.Definitions;
using RpgWorld.Modules.Abstractions.Definitions;

namespace RpgWorld.Modules.Abstractions;

public interface IRpgContentCatalog : IWorldDefinitionCatalog, ITraitDefinitionCatalog
{
    IReadOnlyCollection<RpgModuleMetadata> Modules { get; }
    IReadOnlyCollection<CreatureDefinition> Creatures { get; }
    IReadOnlyCollection<ItemDefinition> Items { get; }
    IReadOnlyCollection<RuleDefinition> Rules { get; }
    CreatureDefinition ResolveCreature(string code);
    ItemDefinition ResolveItem(string code);
    RuleDefinition ResolveRule(string code);
}

public sealed record CampaignModuleSelection(Guid CampaignId, IReadOnlyCollection<string> EnabledModuleIds)
{
    public CampaignModuleSelection(Guid campaignId, params string[] enabledModuleIds)
        : this(campaignId, (IReadOnlyCollection<string>)enabledModuleIds) { }
}

public interface IRpgModuleCatalog
{
    IReadOnlyCollection<RpgModuleMetadata> AvailableModules { get; }
    IRpgContentCatalog Load(IEnumerable<string> moduleIds);
    IRpgContentCatalog ForCampaign(CampaignModuleSelection selection);
}

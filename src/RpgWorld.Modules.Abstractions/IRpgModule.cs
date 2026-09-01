using RpgWorld.Modules.Abstractions.Actors;
using RpgWorld.Modules.Abstractions.Definitions;
using RpgWorld.Modules.Abstractions.Worlds;

namespace RpgWorld.Modules.Abstractions;

public sealed record RpgModuleMetadata
{
    public RpgModuleMetadata(
        string id,
        string name,
        Version version,
        Version minimumEngineVersion,
        Version? maximumEngineVersion = null)
    {
        if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Module identifier is required.", nameof(id));
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Module name is required.", nameof(name));
        ArgumentNullException.ThrowIfNull(version);
        ArgumentNullException.ThrowIfNull(minimumEngineVersion);
        if (maximumEngineVersion is not null && maximumEngineVersion < minimumEngineVersion)
            throw new ArgumentException("Maximum engine version cannot precede the minimum version.", nameof(maximumEngineVersion));
        Id = id.Trim().ToLowerInvariant();
        Name = name.Trim();
        Version = version;
        MinimumEngineVersion = minimumEngineVersion;
        MaximumEngineVersion = maximumEngineVersion;
    }

    public string Id { get; }
    public string Name { get; }
    public Version Version { get; }
    public Version MinimumEngineVersion { get; }
    public Version? MaximumEngineVersion { get; }

    public bool Supports(Version engineVersion) =>
        engineVersion >= MinimumEngineVersion &&
        (MaximumEngineVersion is null || engineVersion <= MaximumEngineVersion);
}

public interface IRpgModule : IWorldDefinitionModule, IActorDefinitionModule
{
    RpgModuleMetadata Metadata { get; }
    IEnumerable<NpcDefinition> Npcs => [];
    IEnumerable<CreatureDefinition> Creatures => [];
    IEnumerable<ItemDefinition> Items => [];
    IEnumerable<RuleDefinition> Rules => [];
}

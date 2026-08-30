namespace RpgWorld.Domain.Worlds.Definitions;

public sealed class WorldDefinitionCatalog : IWorldDefinitionCatalog
{
    private readonly IReadOnlyDictionary<string, TerrainDefinition> _terrains;
    private readonly IReadOnlyDictionary<string, BiomeDefinition> _biomes;
    private readonly IReadOnlyCollection<TerrainDefinition> _terrainValues;
    private readonly IReadOnlyCollection<BiomeDefinition> _biomeValues;

    public WorldDefinitionCatalog(
        IEnumerable<TerrainDefinition> terrains,
        IEnumerable<BiomeDefinition> biomes)
    {
        ArgumentNullException.ThrowIfNull(terrains);
        ArgumentNullException.ThrowIfNull(biomes);

        _terrains = BuildUniqueIndex(terrains, definition => definition.Code, "terrain");
        _biomes = BuildUniqueIndex(biomes, definition => definition.Code, "biome");
        _terrainValues = _terrains.Values.ToArray();
        _biomeValues = _biomes.Values.ToArray();

        foreach (var biome in _biomes.Values)
        {
            if (!_terrains.ContainsKey(biome.TerrainCode))
            {
                throw new ArgumentException(
                    $"Biome '{biome.Code}' references unknown terrain '{biome.TerrainCode}'.",
                    nameof(biomes));
            }
        }
    }

    public IReadOnlyCollection<TerrainDefinition> Terrains => _terrainValues;

    public IReadOnlyCollection<BiomeDefinition> Biomes => _biomeValues;

    public TerrainDefinition ResolveTerrain(string code)
    {
        var normalized = DefinitionCode.Normalize(code, nameof(code));

        return _terrains.TryGetValue(normalized, out var terrain)
            ? terrain
            : throw new KeyNotFoundException($"Terrain definition '{normalized}' was not found.");
    }

    public BiomeDefinition ResolveBiome(string code)
    {
        var normalized = DefinitionCode.Normalize(code, nameof(code));

        return _biomes.TryGetValue(normalized, out var biome)
            ? biome
            : throw new KeyNotFoundException($"Biome definition '{normalized}' was not found.");
    }

    public bool TryResolveTerrain(string code, out TerrainDefinition? terrain) =>
        _terrains.TryGetValue(DefinitionCode.Normalize(code, nameof(code)), out terrain);

    public bool TryResolveBiome(string code, out BiomeDefinition? biome) =>
        _biomes.TryGetValue(DefinitionCode.Normalize(code, nameof(code)), out biome);

    private static IReadOnlyDictionary<string, TDefinition> BuildUniqueIndex<TDefinition>(
        IEnumerable<TDefinition> definitions,
        Func<TDefinition, string> keySelector,
        string kind)
    {
        var result = new Dictionary<string, TDefinition>(StringComparer.Ordinal);

        foreach (var definition in definitions)
        {
            ArgumentNullException.ThrowIfNull(definition);
            var code = keySelector(definition);

            if (!result.TryAdd(code, definition))
            {
                throw new ArgumentException($"Duplicate {kind} definition '{code}'.", nameof(definitions));
            }
        }

        return result;
    }
}

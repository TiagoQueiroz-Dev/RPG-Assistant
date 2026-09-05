using Microsoft.EntityFrameworkCore;
using RpgWorld.Domain.Actors;
using RpgWorld.Domain.Worlds;
using RpgWorld.Domain.Worlds.Cities;
using RpgWorld.Domain.Worlds.Definitions;
using RpgWorld.Domain.Worlds.Factions;
using RpgWorld.Domain.Worlds.Resources;
using RpgWorld.Infrastructure.Persistence;

namespace RpgWorld.Infrastructure.Worlds.Seeding;

public sealed record ReferenceScenarioResult(
    int Seed,
    bool Created,
    Guid WorldId,
    Guid GameMasterWorldId,
    Guid PlayerWorldId,
    Guid PlayerActorId,
    int MapWidth,
    int MapHeight,
    int CityCount,
    int NpcCount,
    int FactionCount,
    int ResourceDepositCount,
    bool SimulationRunning,
    IReadOnlyList<string> CityNames,
    IReadOnlyList<string> FactionNames);

public sealed class ReferenceScenarioSeeder(
    RpgWorldDbContext dbContext,
    IWorldDefinitionCatalog definitions,
    TimeProvider timeProvider)
{
    public const int DefaultSeed = 49_001;
    public const int Width = 64;
    public const int Height = 32;
    public const int ChunkSize = 16;
    public const int NpcCount = 100;

    private static readonly DateTimeOffset ScenarioInstant = DateTimeOffset.UnixEpoch;
    private static readonly string[] Jobs =
    [
        "farmer", "lumberjack", "miner", "merchant", "guard",
        "artisan", "healer", "scholar", "laborer", "innkeeper"
    ];

    public async Task<ReferenceScenarioResult> SeedAsync(
        int seed = DefaultSeed,
        CancellationToken cancellationToken = default)
    {
        var worldName = WorldName(seed);
        var existingWorldId = await dbContext.Worlds.AsNoTracking()
            .Where(world => world.Name == worldName)
            .Select(world => (Guid?)world.Id)
            .SingleOrDefaultAsync(cancellationToken);
        if (existingWorldId is { } existing)
            return await DescribeAsync(existing, seed, created: false, cancellationToken);

        var world = World.Create(worldName, Width, Height, ChunkSize);
        var chunks = CreateChunks(world);
        var tiles = CreateTiles(world, seed);
        var tileIndex = tiles.ToDictionary(tile => (tile.X, tile.Y));
        var northCenter = world.PositionAt(12, 16);
        var southCenter = world.PositionAt(50, 16);
        var northTerritory = Square(world, northCenter, radius: 1);
        var southTerritory = Square(world, southCenter, radius: 1);

        var random = new Random(seed);
        var npcs = Enumerable.Range(0, NpcCount).Select(index =>
        {
            var center = index < NpcCount / 2 ? northCenter : southCenter;
            var position = world.PositionAt(center.X + random.Next(-2, 3), center.Y + random.Next(-2, 3));
            var npc = NpcActor.Create($"Reference NPC {index + 1:000}", world, position, ScenarioInstant);
            npc.AssignJob(Jobs[index % Jobs.Length], ScenarioInstant);
            npc.SetHome(world, center, ScenarioInstant);
            npc.AddInventory("food", 2, ScenarioInstant);
            return npc;
        }).ToArray();

        var northFaction = Faction.Create(
            world, "Northwatch Compact", FactionType.Kingdom, npcs[0].Id, 2_000m, 55m, ScenarioInstant);
        var southFaction = Faction.Create(
            world, "Southroad League", FactionType.MerchantGuild, npcs[NpcCount / 2].Id, 2_500m, 40m,
            ScenarioInstant);
        var factions = new[] { northFaction, southFaction };
        var northCity = City.Create(
            world, "Northwatch", northCenter, northTerritory, 50, 1_000m, ScenarioInstant, northFaction.Id);
        var southCity = City.Create(
            world, "Southroad", southCenter, southTerritory, 50, 1_200m, ScenarioInstant, southFaction.Id);
        var cities = new[] { northCity, southCity };

        for (var index = 0; index < npcs.Length; index++)
        {
            var north = index < NpcCount / 2;
            var faction = north ? northFaction : southFaction;
            var city = north ? northCity : southCity;
            npcs[index].JoinFaction(faction.Id, ScenarioInstant);
            npcs[index].JoinCity(city, ScenarioInstant);
            faction.AddMember(npcs[index].Id, ScenarioInstant);
        }

        northFaction.AssociateCity(northCity.Id, ScenarioInstant);
        southFaction.AssociateCity(southCity.Id, ScenarioInstant);
        northFaction.ClaimTerritory(world, northTerritory, ScenarioInstant);
        southFaction.ClaimTerritory(world, southTerritory, ScenarioInstant);
        var tradeRelation = new FactionRelationModifier(
            FactionRelationModifierSource.Trade, "Reference scenario trade agreement.", affinityDelta: 25);
        northFaction.ApplyRelationModifier(southFaction.Id, tradeRelation, ScenarioInstant);
        southFaction.ApplyRelationModifier(northFaction.Id, tradeRelation, ScenarioInstant);

        var player = PlayerActor.Create("Reference Player", world, northCenter, ScenarioInstant);
        player.SetAttribute("perception", 3, ScenarioInstant);
        player.AddInventory("food", 5, ScenarioInstant);
        foreach (var actor in npcs.Cast<Actor>().Append(player))
            tileIndex[(actor.X, actor.Y)].AddOccupant(actor.Id);
        var resources = CreateResources(world, tileIndex, npcs[0].Id, npcs[NpcCount / 2].Id);
        var knowledge = Square(world, northCenter, radius: 3)
            .Select(position => PlayerTileKnowledge.Discover(
                player.Id, position, known: position == northCenter, ScenarioInstant))
            .ToArray();
        var clock = WorldClock.Create(
            world.Id, ScenarioInstant, timeProvider.GetUtcNow(), TimeSpan.FromHours(1), realTimeMultiplier: 1m);

        dbContext.Add(world);
        dbContext.Chunks.AddRange(chunks);
        dbContext.Tiles.AddRange(tiles);
        dbContext.Actors.AddRange(npcs);
        dbContext.Actors.Add(player);
        dbContext.Cities.AddRange(cities);
        dbContext.Factions.AddRange(factions);
        dbContext.ResourceDeposits.AddRange(resources);
        dbContext.PlayerTileKnowledge.AddRange(knowledge);
        dbContext.WorldClocks.Add(clock);
        await dbContext.SaveChangesAsync(cancellationToken);

        return await DescribeAsync(world.Id, seed, created: true, cancellationToken);
    }

    public static string WorldName(int seed) => $"Aster Reference World (seed {seed})";

    private static Chunk[] CreateChunks(World world) =>
        (from y in Enumerable.Range(0, world.ChunkRows)
         from x in Enumerable.Range(0, world.ChunkColumns)
         select world.CreateChunk(new ChunkCoordinate(x, y))).ToArray();

    private Tile[] CreateTiles(World world, int seed) =>
        (from y in Enumerable.Range(0, world.Height)
         from x in Enumerable.Range(0, world.Width)
         let biome = BiomeAt(x, y, seed)
         select world.CreateTile(
             world.PositionAt(x, y), biome, definitions,
             elevation: ElevationAt(x, y, seed),
             temperatureCelsius: biome == "mountain" ? 8m : biome == "river" ? 18m : 22m,
             humidity: biome == "river" ? 1m : biome == "forest" ? 0.75m : 0.45m)).ToArray();

    private ResourceDeposit[] CreateResources(
        World world,
        IReadOnlyDictionary<(int X, int Y), Tile> tiles,
        Guid northDiscoverer,
        Guid southDiscoverer)
    {
        var placements = new[]
        {
            (11, 15, "food", northDiscoverer), (12, 15, "wood", northDiscoverer),
            (13, 15, "stone", northDiscoverer), (11, 16, "gold", northDiscoverer),
            (49, 15, "food", southDiscoverer), (50, 15, "wood", southDiscoverer),
            (51, 15, "stone", southDiscoverer), (49, 16, "gold", southDiscoverer)
        };
        return placements.Select(placement =>
        {
            var deposit = ResourceDeposit.SpawnOnTile(
                world, tiles[(placement.Item1, placement.Item2)],
                definitions.ResolveResource(placement.Item3), ScenarioInstant);
            deposit.Discover(placement.Item4, ScenarioInstant);
            return deposit;
        }).ToArray();
    }

    private async Task<ReferenceScenarioResult> DescribeAsync(
        Guid worldId,
        int seed,
        bool created,
        CancellationToken cancellationToken)
    {
        var world = await dbContext.Worlds.AsNoTracking()
            .SingleAsync(candidate => candidate.Id == worldId, cancellationToken);
        var playerId = await dbContext.Actors.AsNoTracking().OfType<PlayerActor>()
            .Where(actor => actor.WorldId == worldId)
            .Select(actor => actor.Id)
            .SingleAsync(cancellationToken);
        var cityNames = await dbContext.Cities.AsNoTracking().Where(city => city.WorldId == worldId)
            .OrderBy(city => city.Name).Select(city => city.Name).ToArrayAsync(cancellationToken);
        var factionNames = await dbContext.Factions.AsNoTracking().Where(faction => faction.WorldId == worldId)
            .OrderBy(faction => faction.Name).Select(faction => faction.Name).ToArrayAsync(cancellationToken);
        return new ReferenceScenarioResult(
            seed, created, world.Id, world.Id, world.Id, playerId,
            world.Width, world.Height,
            cityNames.Length,
            await dbContext.Actors.AsNoTracking().OfType<NpcActor>()
                .CountAsync(actor => actor.WorldId == worldId, cancellationToken),
            factionNames.Length,
            await dbContext.ResourceDeposits.AsNoTracking()
                .CountAsync(deposit => deposit.WorldId == worldId, cancellationToken),
            world.IsSimulationRunning,
            cityNames,
            factionNames);
    }

    private static Position[] Square(World world, Position center, int radius) =>
        (from y in Enumerable.Range(center.Y - radius, radius * 2 + 1)
         from x in Enumerable.Range(center.X - radius, radius * 2 + 1)
         select world.PositionAt(x, y)).ToArray();

    private static string BiomeAt(int x, int y, int seed)
    {
        if (x is 31 or 32 && y != 16) return "river";
        if (y < 5 || y > Height - 6) return "mountain";
        return Math.Abs((x * 31L) + (y * 17L) + seed) % 5 == 0 ? "forest" : "grassland";
    }

    private static short ElevationAt(int x, int y, int seed) =>
        checked((short)(Math.Abs((x * 13L) + (y * 7L) + seed) % 180));
}

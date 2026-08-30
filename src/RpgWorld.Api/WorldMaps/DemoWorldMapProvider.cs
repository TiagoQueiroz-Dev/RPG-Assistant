using RpgWorld.Domain.Worlds;
using RpgWorld.Modules.Default.Worlds;

namespace RpgWorld.Api.WorldMaps;

public sealed class DemoWorldMapProvider
{
    private readonly Lazy<WorldMapView> _map = new(CreateMap);

    public WorldMapView GetMap() => _map.Value;

    private static WorldMapView CreateMap()
    {
        var world = World.Create("As Marcas de Aster", width: 96, height: 64);
        var chunks = new List<WorldMapChunkView>(world.ChunkColumns * world.ChunkRows);

        for (var chunkY = 0; chunkY < world.ChunkRows; chunkY++)
        {
            for (var chunkX = 0; chunkX < world.ChunkColumns; chunkX++)
            {
                var chunk = world.CreateChunk(new ChunkCoordinate(chunkX, chunkY));
                var tiles = new List<WorldMapTileView>(chunk.Width * chunk.Height);

                for (var y = chunk.OriginY; y < chunk.OriginY + chunk.Height; y++)
                {
                    for (var x = chunk.OriginX; x < chunk.OriginX + chunk.Width; x++)
                    {
                        var biomeCode = ResolveBiome(x, y);
                        var biome = DefaultWorldDefinitions.Catalog.ResolveBiome(biomeCode);
                        var elevation = ResolveElevation(x, y, biomeCode);

                        tiles.Add(new WorldMapTileView(
                            x,
                            y,
                            biome.TerrainCode,
                            biome.Code,
                            elevation));
                    }
                }

                chunks.Add(new WorldMapChunkView(
                    chunk.Coordinate.X,
                    chunk.Coordinate.Y,
                    chunk.OriginX,
                    chunk.OriginY,
                    chunk.Width,
                    chunk.Height,
                    tiles));
            }
        }

        return new WorldMapView(
            world.Id,
            world.Name,
            world.Width,
            world.Height,
            world.ChunkSize,
            chunks);
    }

    private static string ResolveBiome(int x, int y)
    {
        if (x < 10)
        {
            return "ocean";
        }

        if (x is >= 46 and <= 48)
        {
            return "river";
        }

        if (x > 80 && y < 22)
        {
            return "volcanic";
        }

        if (y < 8)
        {
            return "snow";
        }

        if (y < 18 || ((x * 17 + y * 31) % 47) < 5)
        {
            return "mountain";
        }

        if (x > 70)
        {
            return "desert";
        }

        if (y > 48 && x < 46)
        {
            return "swamp";
        }

        if (x < 36)
        {
            return "forest";
        }

        return "grassland";
    }

    private static short ResolveElevation(int x, int y, string biomeCode)
    {
        var variation = ((x * 13 + y * 7) % 19) - 9;
        var baseline = biomeCode switch
        {
            "ocean" => -80,
            "river" => -4,
            "mountain" => 180,
            "volcanic" => 260,
            "swamp" => 2,
            _ => 24
        };

        return (short)(baseline + variation);
    }
}

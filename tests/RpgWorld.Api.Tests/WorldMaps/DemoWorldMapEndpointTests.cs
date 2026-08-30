using System.Net.Http.Json;
using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using RpgWorld.Api.WorldMaps;

namespace RpgWorld.Api.Tests.WorldMaps;

public sealed class DemoWorldMapEndpointTests
{
    [Fact]
    public async Task Returns_world_with_multiple_complete_chunks_and_distinct_biomes()
    {
        using var factory = new MapWebApplicationFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/worlds/demo/map");
        response.EnsureSuccessStatusCode();
        var map = await response.Content.ReadFromJsonAsync<WorldMapView>();

        Assert.NotNull(map);
        Assert.Equal(96, map.Width);
        Assert.Equal(64, map.Height);
        Assert.Equal(32, map.ChunkSize);
        Assert.Equal(6, map.Chunks.Count);
        Assert.All(map.Chunks, chunk => Assert.Equal(1024, chunk.Tiles.Count));
        Assert.Equal(96 * 64, map.Chunks.Sum(chunk => chunk.Tiles.Count));

        var biomes = map.Chunks
            .SelectMany(chunk => chunk.Tiles)
            .Select(tile => tile.BiomeCode)
            .ToHashSet(StringComparer.Ordinal);

        Assert.Equal(9, biomes.Count);
        Assert.Contains("ocean", biomes);
        Assert.Contains("volcanic", biomes);
    }

    [Fact]
    public async Task Import_endpoint_rejects_invalid_image_without_server_error()
    {
        using var factory = new MapWebApplicationFactory();
        using var client = factory.CreateClient();
        using var content = new MultipartFormDataContent();
        content.Add(new StringContent("Invalid map"), "name");
        content.Add(new StringContent("32"), "gridResolution");
        content.Add(new ByteArrayContent([1, 2, 3, 4]), "file", "map.png");

        var response = await client.PostAsync("/worlds/import", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Paint_endpoint_rejects_unknown_brush_before_persistence()
    {
        using var factory = new MapWebApplicationFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            $"/api/worlds/{Guid.NewGuid()}/map/paint",
            new { brush = "lava-laser", centerX = 0, centerY = 0, size = 1 });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private sealed class MapWebApplicationFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, configuration) =>
            {
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:RpgWorld"] =
                        "Host=localhost;Database=unused;Username=unused",
                    ["Redis:Enabled"] = "false"
                });
            });
        }
    }
}

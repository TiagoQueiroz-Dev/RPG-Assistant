using System.Net.Http.Json;
using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.AspNetCore.Builder;
using System.Security.Claims;
using RpgWorld.Api.Realtime;
using RpgWorld.Api.WorldMaps;
using RpgWorld.Simulation.Time;

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

    [Fact]
    public async Task Every_time_command_rejects_requests_without_game_master_context()
    {
        using var factory = new MapWebApplicationFactory();
        using var client = factory.CreateClient();
        var worldId = Guid.NewGuid();
        var requests = new[]
        {
            new HttpRequestMessage(HttpMethod.Post, $"/api/worlds/{worldId}/simulation/start"),
            new HttpRequestMessage(HttpMethod.Post, $"/api/worlds/{worldId}/simulation/pause"),
            new HttpRequestMessage(HttpMethod.Post, $"/api/worlds/{worldId}/clock/ticks/1"),
            JsonRequest(HttpMethod.Put, $"/api/worlds/{worldId}/clock/configuration", new { tickDurationSeconds = 60, realTimeMultiplier = 1 }),
            new HttpRequestMessage(HttpMethod.Post, $"/api/worlds/{worldId}/time/pause"),
            new HttpRequestMessage(HttpMethod.Post, $"/api/worlds/{worldId}/time/resume"),
            JsonRequest(HttpMethod.Put, $"/api/worlds/{worldId}/time/speed", new { realTimeMultiplier = 2 }),
            JsonRequest(HttpMethod.Post, $"/api/worlds/{worldId}/time/advance", new { durationSeconds = 3600 })
        };

        foreach (var request in requests)
        {
            using (request)
            using (var response = await client.SendAsync(request))
            {
                Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            }
        }
    }

    [Fact]
    public async Task Time_command_accepts_matching_game_master_context()
    {
        using var factory = new MapWebApplicationFactory();
        using var client = factory.CreateClient();
        var worldId = Guid.NewGuid();
        client.DefaultRequestHeaders.Add("X-Test-Game-Master-World", worldId.ToString());

        using var response = await client.PostAsync($"/api/worlds/{worldId}/time/pause", null);

        response.EnsureSuccessStatusCode();
        Assert.Equal(
            worldId,
            factory.Services.GetRequiredService<RecordingTimeCommandService>().PausedWorldId);
    }

    private static HttpRequestMessage JsonRequest(HttpMethod method, string uri, object body) =>
        new(method, uri) { Content = JsonContent.Create(body) };

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
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IWorldTimeCommandService>();
                services.AddSingleton<RecordingTimeCommandService>();
                services.AddSingleton<IWorldTimeCommandService>(provider =>
                    provider.GetRequiredService<RecordingTimeCommandService>());
                services.AddSingleton<IStartupFilter, GameMasterClaimStartupFilter>();
            });
        }
    }

    private sealed class GameMasterClaimStartupFilter : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.Use(async (context, continuePipeline) =>
            {
                if (Guid.TryParse(context.Request.Headers["X-Test-Game-Master-World"], out var worldId))
                {
                    context.User = new ClaimsPrincipal(new ClaimsIdentity(
                        [new Claim(RealtimeClaimTypes.GameMasterWorld, worldId.ToString())],
                        "test"));
                }
                await continuePipeline();
            });
            next(app);
        };
    }

    private sealed class RecordingTimeCommandService : IWorldTimeCommandService
    {
        public Guid? PausedWorldId { get; private set; }

        public Task<WorldTimeCommandResult> PauseAsync(Guid worldId, CancellationToken cancellationToken = default)
        {
            PausedWorldId = worldId;
            return Task.FromResult(Result(worldId, false, "paused"));
        }

        public Task<WorldTimeCommandResult> ResumeAsync(Guid worldId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result(worldId, true, "resumed"));

        public Task<WorldTimeCommandResult> SetMultiplierAsync(Guid worldId, decimal multiplier, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result(worldId, true, "speed-changed") with { RealTimeMultiplier = multiplier });

        public Task<WorldTimeCommandResult> ConfigureAsync(Guid worldId, TimeSpan tickDuration, decimal multiplier, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result(worldId, true, "configured") with
            {
                TickDuration = tickDuration,
                RealTimeMultiplier = multiplier
            });

        public Task<WorldTimeCommandResult> AdvanceAsync(Guid worldId, TimeSpan duration, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result(worldId, true, "advanced") with
            {
                CurrentInstant = DateTimeOffset.UnixEpoch.Add(duration)
            });

        public Task<WorldTimeCommandResult> AdvanceTicksAsync(Guid worldId, int tickCount, CancellationToken cancellationToken = default) =>
            AdvanceAsync(worldId, TimeSpan.FromMinutes(tickCount), cancellationToken);

        private static WorldTimeCommandResult Result(Guid worldId, bool running, string command) =>
            new(worldId, running, DateTimeOffset.UnixEpoch, TimeSpan.FromMinutes(1), 1m, command);
    }
}

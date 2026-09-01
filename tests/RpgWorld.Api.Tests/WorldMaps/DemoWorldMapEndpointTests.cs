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
using RpgWorld.Application.Actors.Movement;
using RpgWorld.Domain.Worlds;
using RpgWorld.Application.Actors.Inspection;
using RpgWorld.Application.Worlds.Admin;

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
            new HttpRequestMessage(HttpMethod.Get, $"/api/worlds/{worldId}/events?page=1&pageSize=20"),
            new HttpRequestMessage(HttpMethod.Get, $"/api/worlds/{worldId}/admin?entityType=chunks&page=1&pageSize=20"),
            new HttpRequestMessage(HttpMethod.Get, $"/api/worlds/{worldId}/map/layers/Population"),
            new HttpRequestMessage(HttpMethod.Get, $"/api/worlds/{worldId}/map"),
            new HttpRequestMessage(HttpMethod.Get, $"/api/worlds/{worldId}/players/{Guid.NewGuid()}/map"),
            new HttpRequestMessage(HttpMethod.Get, $"/api/worlds/{worldId}/players/{Guid.NewGuid()}/view"),
            new HttpRequestMessage(HttpMethod.Get, $"/api/worlds/{worldId}/actors?x=0&y=0"),
            new HttpRequestMessage(HttpMethod.Get, $"/api/worlds/{worldId}/cities"),
            new HttpRequestMessage(HttpMethod.Get, $"/api/worlds/{worldId}/factions"),
            new HttpRequestMessage(HttpMethod.Get, $"/api/actors/{Guid.NewGuid()}/inspector"),
            new HttpRequestMessage(HttpMethod.Get, $"/api/cities/{Guid.NewGuid()}"),
            new HttpRequestMessage(HttpMethod.Get, $"/api/factions/{Guid.NewGuid()}"),
            JsonRequest(HttpMethod.Post, $"/api/actors/{Guid.NewGuid()}/move", new { destinationX = 1, destinationY = 1 }),
            JsonRequest(HttpMethod.Post, $"/api/worlds/{worldId}/admin/commands", new
            {
                action = "CreateEvent",
                eventType = "unauthorized",
                eventPayload = "{}"
            }),
            new HttpRequestMessage(HttpMethod.Post, $"/api/worlds/{worldId}/clock/ticks/1"),
            JsonRequest(HttpMethod.Post, $"/api/worlds/{worldId}/cities", new
            {
                name = "Unauthorized",
                centerX = 0,
                centerY = 0,
                territory = new[] { new { x = 0, y = 0 } },
                initialPopulation = 1,
                initialWealth = 0,
                foundedAtUtc = DateTimeOffset.UnixEpoch
            }),
            JsonRequest(HttpMethod.Post, $"/api/worlds/{worldId}/factions", new
            {
                name = "Unauthorized faction",
                type = "Guild",
                leaderActorId = Guid.NewGuid(),
                initialWealth = 0,
                initialMilitaryPower = 0,
                createdAtUtc = DateTimeOffset.UnixEpoch,
                territory = Array.Empty<object>()
            }),
            JsonRequest(HttpMethod.Post,
                $"/api/worlds/{worldId}/factions/{Guid.NewGuid()}/relations/{Guid.NewGuid()}/modifiers",
                new
                {
                    source = "Event",
                    reason = "Unauthorized diplomacy",
                    affinityDelta = -10,
                    tensionDelta = 10,
                    occurredAtUtc = DateTimeOffset.UnixEpoch
                }),
            JsonRequest(HttpMethod.Post,
                $"/api/worlds/{worldId}/factions/{Guid.NewGuid()}/wars/{Guid.NewGuid()}/force",
                new { reason = "Unauthorized war", occurredAtUtc = DateTimeOffset.UnixEpoch }),
            JsonRequest(HttpMethod.Post,
                $"/api/worlds/{worldId}/factions/{Guid.NewGuid()}/wars/{Guid.NewGuid()}/prevent",
                new
                {
                    preventedUntilUtc = DateTimeOffset.UnixEpoch.AddDays(1),
                    reason = "Unauthorized prevention",
                    occurredAtUtc = DateTimeOffset.UnixEpoch
                }),
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

    [Fact]
    public async Task Game_master_command_accepts_matching_context_and_maps_typed_payload()
    {
        using var factory = new MapWebApplicationFactory();
        using var client = factory.CreateClient();
        var worldId = Guid.NewGuid();
        client.DefaultRequestHeaders.Add("X-Test-Game-Master-World", worldId.ToString());

        using var response = await client.PostAsJsonAsync($"/api/worlds/{worldId}/admin/commands", new
        {
            action = "CreateNpc",
            name = "Cartographer",
            x = 4,
            y = 6,
            maximumHealth = 80
        });

        response.EnsureSuccessStatusCode();
        var recorded = factory.Services.GetRequiredService<RecordingGameMasterCommandService>();
        Assert.Equal(worldId, recorded.WorldId);
        Assert.Equal(GameMasterCommandType.CreateNpc, recorded.Command?.Type);
        Assert.Equal((4, 6, 80), (recorded.Command?.X, recorded.Command?.Y, recorded.Command?.MaximumHealth));
    }

    [Fact]
    public async Task Player_cannot_change_actor_identifier_to_read_another_world_view()
    {
        using var factory = new MapWebApplicationFactory();
        using var client = factory.CreateClient();
        var worldId = Guid.NewGuid();
        var playerActorId = Guid.NewGuid();
        client.DefaultRequestHeaders.Add("X-Test-Player-World", worldId.ToString());
        client.DefaultRequestHeaders.Add("X-Test-Player-Actor", playerActorId.ToString());

        using var response = await client.GetAsync(
            $"/api/worlds/{worldId}/players/{Guid.NewGuid()}/view");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Actor_move_endpoint_forwards_destination_to_shared_movement_service()
    {
        using var factory = new MapWebApplicationFactory();
        using var client = factory.CreateClient();
        var actorId = Guid.NewGuid();
        client.DefaultRequestHeaders.Add("X-Test-Player-Actor", actorId.ToString());

        using var response = await client.PostAsJsonAsync(
            $"/api/actors/{actorId}/move",
            new { destinationX = 7, destinationY = 9 });

        response.EnsureSuccessStatusCode();
        Assert.Equal(
            new ActorMoveRequest(actorId, 7, 9),
            factory.Services.GetRequiredService<RecordingActorMovementService>().LastRequest);
    }

    [Fact]
    public async Task Npc_inspector_endpoints_expose_tile_actors_and_trait_effects()
    {
        using var factory = new MapWebApplicationFactory();
        using var client = factory.CreateClient();
        var inspectorService = factory.Services.GetRequiredService<RecordingNpcInspectorService>();
        var worldId = inspectorService.WorldId;
        var actorId = inspectorService.ActorId;
        client.DefaultRequestHeaders.Add("X-Test-Game-Master-World", worldId.ToString());

        var actors = await client.GetFromJsonAsync<ActorAtPositionView[]>(
            $"/api/worlds/{worldId}/actors?x=4&y=6");
        var inspector = await client.GetFromJsonAsync<NpcInspectorView>(
            $"/api/actors/{actorId}/inspector");

        Assert.Equal(actorId, Assert.Single(actors!).ActorId);
        var trait = Assert.Single(inspector!.Traits);
        Assert.Equal("brave", trait.Code);
        Assert.Equal(1.35m, trait.ActionScoreMultipliers["AttackEnemy"]);
        Assert.Equal((worldId, 4, 6), factory.Services
            .GetRequiredService<RecordingNpcInspectorService>().LastPosition);
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
                services.RemoveAll<IActorMovementService>();
                services.AddSingleton<RecordingActorMovementService>();
                services.AddSingleton<IActorMovementService>(provider =>
                    provider.GetRequiredService<RecordingActorMovementService>());
                services.RemoveAll<INpcInspectorService>();
                services.AddSingleton<RecordingNpcInspectorService>();
                services.AddSingleton<INpcInspectorService>(provider =>
                    provider.GetRequiredService<RecordingNpcInspectorService>());
                services.RemoveAll<IGameMasterCommandService>();
                services.AddSingleton<RecordingGameMasterCommandService>();
                services.AddSingleton<IGameMasterCommandService>(provider =>
                    provider.GetRequiredService<RecordingGameMasterCommandService>());
            });
        }
    }

    private sealed class GameMasterClaimStartupFilter : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.Use(async (context, continuePipeline) =>
            {
                var claims = new List<Claim>();
                if (Guid.TryParse(context.Request.Headers["X-Test-Game-Master-World"], out var worldId))
                    claims.Add(new Claim(RealtimeClaimTypes.GameMasterWorld, worldId.ToString()));
                if (Guid.TryParse(context.Request.Headers["X-Test-Player-Actor"], out var playerActorId))
                    claims.Add(new Claim(RealtimeClaimTypes.PlayerActor, playerActorId.ToString()));
                if (Guid.TryParse(context.Request.Headers["X-Test-Player-World"], out var playerWorldId))
                    claims.Add(new Claim(RealtimeClaimTypes.World, playerWorldId.ToString()));
                if (claims.Count > 0) context.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));
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

    private sealed class RecordingActorMovementService : IActorMovementService
    {
        public ActorMoveRequest? LastRequest { get; private set; }

        public Task<ActorMoveResult> MoveAsync(
            ActorMoveRequest request,
            CancellationToken cancellationToken = default)
        {
            LastRequest = request;
            var worldId = Guid.NewGuid();
            var origin = new Position(worldId, 6, 9);
            var destination = new Position(worldId, request.DestinationX, request.DestinationY);
            return Task.FromResult(new ActorMoveResult(
                request.ActorId,
                origin,
                destination,
                Guid.NewGuid(),
                Guid.NewGuid(),
                false,
                1m));
        }
    }

    private sealed class RecordingNpcInspectorService : INpcInspectorService
    {
        public Guid ActorId { get; } = Guid.NewGuid();
        public Guid WorldId { get; } = Guid.NewGuid();
        public (Guid WorldId, int X, int Y)? LastPosition { get; private set; }

        public Task<IReadOnlyList<ActorAtPositionView>> ListAtPositionAsync(
            Guid worldId,
            int x,
            int y,
            CancellationToken cancellationToken = default)
        {
            LastPosition = (worldId, x, y);
            return Task.FromResult<IReadOnlyList<ActorAtPositionView>>([
                new ActorAtPositionView(ActorId, "Brave NPC", "npc", "AttackEnemy", ["brave"])
            ]);
        }

        public Task<NpcInspectorView?> GetNpcAsync(
            Guid actorId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<NpcInspectorView?>(actorId != ActorId ? null : new NpcInspectorView(
                ActorId,
                WorldId,
                "Brave NPC",
                4,
                6,
                100,
                100,
                10m,
                80m,
                25m,
                "guard",
                "AttackEnemy",
                null,
                [new NpcTraitInspectorView(
                    "brave",
                    "Brave",
                    "Faces danger.",
                    new Dictionary<string, decimal> { ["AttackEnemy"] = 1.35m },
                    true)],
                [new NpcMemoryInspectorView(
                    Guid.NewGuid(),
                    "family-member-killed",
                    Guid.NewGuid(),
                    100,
                    DateTimeOffset.UnixEpoch,
                    null,
                    new Dictionary<string, string>())],
                []));
    }

    private sealed class RecordingGameMasterCommandService : IGameMasterCommandService
    {
        public Guid? WorldId { get; private set; }
        public GameMasterCommand? Command { get; private set; }

        public Task<GameMasterCommandResult> ExecuteAsync(
            Guid worldId, GameMasterCommand command, CancellationToken cancellationToken = default)
        {
            WorldId = worldId;
            Command = command;
            return Task.FromResult(new GameMasterCommandResult(
                Guid.NewGuid(), worldId, command.Type.ToString(), Guid.NewGuid(),
                DateTimeOffset.UnixEpoch, "recorded"));
        }
    }
}

using System.Security.Claims;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RpgWorld.Api.Realtime;
using RpgWorld.Application.Realtime;
using RpgWorld.Application.Worlds.Visibility;

namespace RpgWorld.Api.Tests.Realtime;

public sealed class WorldHubTests
{
    [Fact]
    public async Task Client_connects_joins_authorized_group_and_receives_world_update()
    {
        using var factory = new RealtimeWebApplicationFactory();
        var worldId = Guid.NewGuid();
        var received = new TaskCompletionSource<WorldUpdateMessage>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        await using var connection = new HubConnectionBuilder()
            .WithUrl("http://localhost/hubs/world", options =>
            {
                options.Transports = HttpTransportType.LongPolling;
                options.HttpMessageHandlerFactory = _ => factory.Server.CreateHandler();
            })
            .WithAutomaticReconnect([
                TimeSpan.Zero,
                TimeSpan.FromSeconds(2),
                TimeSpan.FromSeconds(10),
                TimeSpan.FromSeconds(30)])
            .Build();

        connection.On<WorldUpdateMessage>(
            nameof(IWorldHubClient.WorldUpdated),
            message => received.TrySetResult(message));

        await connection.StartAsync();
        Assert.Equal(HubConnectionState.Connected, connection.State);

        await connection.InvokeAsync(nameof(WorldHub.JoinWorld), worldId);

        var message = new WorldUpdateMessage(
            Guid.NewGuid(),
            worldId,
            "city.created",
            DateTimeOffset.UtcNow,
            new Dictionary<string, string?> { ["cityId"] = Guid.NewGuid().ToString() });

        using var scope = factory.Services.CreateScope();
        var publisher = scope.ServiceProvider.GetRequiredService<IWorldUpdatePublisher>();
        await publisher.PublishToWorldAsync(message);

        var delivered = await received.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(message.MessageId, delivered.MessageId);
        Assert.Equal(message.WorldId, delivered.WorldId);
        Assert.Equal(message.UpdateType, delivered.UpdateType);
        Assert.Equal(message.Data["cityId"], delivered.Data["cityId"]);

        await connection.InvokeAsync(nameof(WorldHub.LeaveWorld), worldId);
        await connection.StopAsync();
    }

    [Fact]
    public async Task Positional_update_is_sent_only_to_players_selected_by_server_visibility()
    {
        using var factory = new RealtimeWebApplicationFactory();
        var worldId = Guid.NewGuid();
        var playerId = Guid.NewGuid();
        factory.Services.GetRequiredService<RecordingVisibilityService>().Recipients = [playerId];
        var received = new TaskCompletionSource<WorldUpdateMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var connection = new HubConnectionBuilder()
            .WithUrl("http://localhost/hubs/world", options =>
            {
                options.Transports = HttpTransportType.LongPolling;
                options.HttpMessageHandlerFactory = _ => factory.Server.CreateHandler();
            }).Build();
        connection.On<WorldUpdateMessage>(nameof(IWorldHubClient.WorldUpdated), message => received.TrySetResult(message));
        await connection.StartAsync();
        await connection.InvokeAsync(nameof(WorldHub.JoinPlayer), playerId);
        using var scope = factory.Services.CreateScope();
        var publisher = scope.ServiceProvider.GetRequiredService<IWorldUpdatePublisher>();
        var message = new WorldUpdateMessage(Guid.NewGuid(), worldId, "actor.moved", DateTimeOffset.UtcNow,
            new Dictionary<string, string?> { ["actorId"] = Guid.NewGuid().ToString(), ["destinationX"] = "7", ["destinationY"] = "9" });

        await publisher.PublishToGameMasterAsync(message);

        Assert.Equal(message.MessageId, (await received.Task.WaitAsync(TimeSpan.FromSeconds(5))).MessageId);
        Assert.Equal((worldId, 7, 9), factory.Services.GetRequiredService<RecordingVisibilityService>().LastQuery);
    }

    [Fact]
    public async Task Movement_invalidates_origin_view_without_revealing_hidden_destination()
    {
        using var factory = new RealtimeWebApplicationFactory();
        var worldId = Guid.NewGuid();
        var originPlayerId = Guid.NewGuid();
        var destinationPlayerId = Guid.NewGuid();
        factory.Services.GetRequiredService<RecordingVisibilityService>().RecipientSelector = (x, y) =>
            (x, y) == (1, 1) ? [originPlayerId] : [destinationPlayerId];
        var originReceived = new TaskCompletionSource<WorldUpdateMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        var destinationReceived = new TaskCompletionSource<WorldUpdateMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var origin = Connection(factory, originReceived);
        await using var destination = Connection(factory, destinationReceived);
        await origin.StartAsync();
        await destination.StartAsync();
        await origin.InvokeAsync(nameof(WorldHub.JoinPlayer), originPlayerId);
        await destination.InvokeAsync(nameof(WorldHub.JoinPlayer), destinationPlayerId);
        using var scope = factory.Services.CreateScope();
        var message = new WorldUpdateMessage(Guid.NewGuid(), worldId, "actor.moved", DateTimeOffset.UtcNow,
            new Dictionary<string, string?>
            {
                ["actorId"] = Guid.NewGuid().ToString(), ["actorKind"] = "npc",
                ["originX"] = "1", ["originY"] = "1", ["destinationX"] = "9", ["destinationY"] = "9"
            });

        await scope.ServiceProvider.GetRequiredService<IWorldUpdatePublisher>().PublishToGameMasterAsync(message);

        var entered = await destinationReceived.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var left = await originReceived.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal("actor.moved", entered.UpdateType);
        Assert.Equal("visibility.changed", left.UpdateType);
        Assert.DoesNotContain("destinationX", left.Data.Keys);
        Assert.DoesNotContain("destinationY", left.Data.Keys);
    }

    private static HubConnection Connection(
        RealtimeWebApplicationFactory factory,
        TaskCompletionSource<WorldUpdateMessage> received)
    {
        var connection = new HubConnectionBuilder()
            .WithUrl("http://localhost/hubs/world", options =>
            {
                options.Transports = HttpTransportType.LongPolling;
                options.HttpMessageHandlerFactory = _ => factory.Server.CreateHandler();
            }).Build();
        connection.On<WorldUpdateMessage>(nameof(IWorldHubClient.WorldUpdated), message => received.TrySetResult(message));
        return connection;
    }

    [Fact]
    public async Task Claim_authorizer_only_allows_claimed_audiences()
    {
        var worldId = Guid.NewGuid();
        var otherWorldId = Guid.NewGuid();
        var playerActorId = Guid.NewGuid();
        var playerIdentity = new ClaimsIdentity(
            [
                new Claim(RealtimeClaimTypes.World, worldId.ToString()),
                new Claim(RealtimeClaimTypes.PlayerActor, playerActorId.ToString())
            ],
            authenticationType: "test");
        var player = new ClaimsPrincipal(playerIdentity);
        var master = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(RealtimeClaimTypes.GameMasterWorld, worldId.ToString())], "test"));
        var authorizer = new ClaimBasedRealtimeSubscriptionAuthorizer();

        Assert.False(await authorizer.CanSubscribeAsync(
            player,
            new RealtimeSubscription(RealtimeAudience.World, worldId)));
        Assert.False(await authorizer.CanSubscribeAsync(
            player,
            new RealtimeSubscription(RealtimeAudience.World, otherWorldId)));
        Assert.False(await authorizer.CanSubscribeAsync(
            player,
            new RealtimeSubscription(RealtimeAudience.GameMaster, worldId)));
        Assert.True(await authorizer.CanSubscribeAsync(
            player,
            new RealtimeSubscription(RealtimeAudience.Player, playerActorId)));
        Assert.False(await authorizer.CanSubscribeAsync(
            player,
            new RealtimeSubscription(RealtimeAudience.Chunk, Guid.NewGuid())));
        Assert.True(await authorizer.CanSubscribeAsync(
            master,
            new RealtimeSubscription(RealtimeAudience.World, worldId)));
    }

    private sealed class RealtimeWebApplicationFactory : WebApplicationFactory<Program>
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
                services.RemoveAll<IRealtimeSubscriptionAuthorizer>();
                services.AddSingleton<IRealtimeSubscriptionAuthorizer, AllowAllAuthorizer>();
                services.RemoveAll<IPlayerVisibilityService>();
                services.AddSingleton<RecordingVisibilityService>();
                services.AddSingleton<IPlayerVisibilityService>(provider =>
                    provider.GetRequiredService<RecordingVisibilityService>());
            });
        }
    }

    private sealed class AllowAllAuthorizer : IRealtimeSubscriptionAuthorizer
    {
        public Task<bool> CanSubscribeAsync(
            ClaimsPrincipal? user,
            RealtimeSubscription subscription,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(true);
    }

    private sealed class RecordingVisibilityService : IPlayerVisibilityService
    {
        public IReadOnlyList<Guid> Recipients { get; set; } = [];
        public Func<int, int, IReadOnlyList<Guid>>? RecipientSelector { get; set; }
        public (Guid WorldId, int X, int Y)? LastQuery { get; private set; }
        public Task<PlayerVisibilityView> GetAsync(Guid playerActorId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
        public Task RefreshAsync(Guid playerActorId, DateTimeOffset observedAtUtc, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
        public Task<IReadOnlyList<Guid>> ListPlayersSeeingAsync(
            Guid worldId, int x, int y, CancellationToken cancellationToken = default)
        {
            LastQuery = (worldId, x, y);
            return Task.FromResult(RecipientSelector?.Invoke(x, y) ?? Recipients);
        }
    }
}

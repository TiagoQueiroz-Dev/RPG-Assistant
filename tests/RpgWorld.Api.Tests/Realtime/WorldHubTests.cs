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

        var publisher = factory.Services.GetRequiredService<IWorldUpdatePublisher>();
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
    public async Task Claim_authorizer_only_allows_claimed_audiences()
    {
        var worldId = Guid.NewGuid();
        var otherWorldId = Guid.NewGuid();
        var identity = new ClaimsIdentity(
            [new Claim(RealtimeClaimTypes.World, worldId.ToString())],
            authenticationType: "test");
        var user = new ClaimsPrincipal(identity);
        var authorizer = new ClaimBasedRealtimeSubscriptionAuthorizer();

        Assert.True(await authorizer.CanSubscribeAsync(
            user,
            new RealtimeSubscription(RealtimeAudience.World, worldId)));
        Assert.False(await authorizer.CanSubscribeAsync(
            user,
            new RealtimeSubscription(RealtimeAudience.World, otherWorldId)));
        Assert.False(await authorizer.CanSubscribeAsync(
            user,
            new RealtimeSubscription(RealtimeAudience.GameMaster, worldId)));
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
}

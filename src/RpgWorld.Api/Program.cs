using RpgWorld.Api.Realtime;
using RpgWorld.Application.Realtime;
using RpgWorld.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddSignalR(options =>
{
    options.ClientTimeoutInterval = TimeSpan.FromSeconds(30);
    options.KeepAliveInterval = TimeSpan.FromSeconds(15);
});
builder.Services.AddSingleton<
    IRealtimeSubscriptionAuthorizer,
    ClaimBasedRealtimeSubscriptionAuthorizer>();
builder.Services.AddSingleton<IWorldUpdatePublisher, SignalRWorldUpdatePublisher>();

var app = builder.Build();

app.MapHub<WorldHub>("/hubs/world", options =>
{
    options.AllowStatefulReconnects = true;
});

var summaries = new[]
{
    "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
};

app.MapGet("/weatherforecast", () =>
{
    var forecast =  Enumerable.Range(1, 5).Select(index =>
        new WeatherForecast
        (
            DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
            Random.Shared.Next(-20, 55),
            summaries[Random.Shared.Next(summaries.Length)]
        ))
        .ToArray();
    return forecast;
});

app.Run();

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}

public partial class Program
{
}

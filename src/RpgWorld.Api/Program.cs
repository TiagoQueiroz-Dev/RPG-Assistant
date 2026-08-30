using RpgWorld.Api.Realtime;
using RpgWorld.Api.WorldMaps;
using RpgWorld.Application.Realtime;
using RpgWorld.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddInfrastructure(builder.Configuration);
var frontendOrigins = builder.Configuration
    .GetSection("Frontend:AllowedOrigins")
    .GetChildren()
    .Select(origin => origin.Value)
    .OfType<string>()
    .ToArray();
builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
    {
        if (frontendOrigins.Length > 0)
        {
            policy.WithOrigins(frontendOrigins)
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials();
        }
    });
});
builder.Services.AddSignalR(options =>
{
    options.ClientTimeoutInterval = TimeSpan.FromSeconds(30);
    options.KeepAliveInterval = TimeSpan.FromSeconds(15);
});
builder.Services.AddSingleton<
    IRealtimeSubscriptionAuthorizer,
    ClaimBasedRealtimeSubscriptionAuthorizer>();
builder.Services.AddSingleton<IWorldUpdatePublisher, SignalRWorldUpdatePublisher>();
builder.Services.AddSingleton<DemoWorldMapProvider>();

var app = builder.Build();

app.UseCors("Frontend");

app.MapHub<WorldHub>("/hubs/world", options =>
{
    options.AllowStatefulReconnects = true;
});

app.MapGet("/api/worlds/demo/map", (DemoWorldMapProvider provider) =>
        Results.Ok(provider.GetMap()))
    .WithName("GetDemoWorldMap");

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

using RpgWorld.Api.Realtime;
using RpgWorld.Api.WorldMaps;
using RpgWorld.Application.Realtime;
using RpgWorld.Application.Worlds.Importing;
using RpgWorld.Application.Worlds.Editing;
using RpgWorld.Domain.Worlds.Definitions;
using RpgWorld.Infrastructure;
using RpgWorld.Infrastructure.Worlds.Importing;
using RpgWorld.Modules.Default.Worlds;
using RpgWorld.Simulation;
using Microsoft.AspNetCore.Http.Features;
using RpgWorld.Simulation.Time;
using RpgWorld.Simulation.Engine;
using System.Globalization;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<IWorldDefinitionCatalog>(DefaultWorldDefinitions.Catalog);
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddSimulation(
    simulationEngineOptions: new SimulationEngineOptions
    {
        TickInterval = TimeSpan.FromMilliseconds(
            builder.Configuration.GetValue<double?>("Simulation:TickIntervalMilliseconds")
            ?? SimulationEngineOptions.DefaultTickInterval.TotalMilliseconds),
        SystemFrequencyOverrides = builder.Configuration
            .GetSection("Simulation:SystemFrequencyOverrides")
            .GetChildren()
            .ToDictionary(
                entry => entry.Key,
                entry => TimeSpan.Parse(entry.Value!, CultureInfo.InvariantCulture),
                StringComparer.OrdinalIgnoreCase)
    });
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
builder.Services.AddScoped<PersistedWorldMapProvider>();
builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = WorldImportService.MaximumFileSize + (128 * 1024);
});

var app = builder.Build();

app.UseCors("Frontend");

app.MapHub<WorldHub>("/hubs/world", options =>
{
    options.AllowStatefulReconnects = true;
});

app.MapGet("/api/worlds/demo/map", (DemoWorldMapProvider provider) =>
        Results.Ok(provider.GetMap()))
    .WithName("GetDemoWorldMap");

app.MapGet(
        "/api/worlds/{worldId:guid}/map",
        async (Guid worldId, PersistedWorldMapProvider provider, CancellationToken cancellationToken) =>
        {
            var map = await provider.GetMapAsync(worldId, cancellationToken);
            return map is null ? Results.NotFound() : Results.Ok(map);
        })
    .WithName("GetPersistedWorldMap");

app.MapPost("/worlds/import", ImportWorldAsync)
    .DisableAntiforgery()
    .WithName("ImportWorld");
app.MapPost("/api/worlds/import", ImportWorldAsync)
    .DisableAntiforgery()
    .WithName("ImportWorldApi");

app.MapPost(
    "/api/worlds/{worldId:guid}/classification/reprocess",
    async (Guid worldId, IWorldClassificationService service, CancellationToken cancellationToken) =>
    {
        try
        {
            return Results.Ok(await service.ReprocessAsync(worldId, cancellationToken));
        }
        catch (KeyNotFoundException)
        {
            return Results.NotFound();
        }
    });

app.MapGet(
    "/api/worlds/{worldId:guid}/simulation/diagnostics",
    (Guid worldId, ISimulationScheduler scheduler) =>
        Results.Ok(scheduler.GetDiagnostics(worldId)));

app.MapPut(
    "/api/worlds/{worldId:guid}/tiles/{x:int}/{y:int}/biome",
    async (Guid worldId, int x, int y, ManualBiomeRequest body, IWorldClassificationService service, CancellationToken cancellationToken) =>
    {
        try
        {
            await service.ConfirmManualAsync(worldId, x, y, body.BiomeCode, cancellationToken);
            return Results.NoContent();
        }
        catch (KeyNotFoundException)
        {
            return Results.NotFound();
        }
        catch (ArgumentException exception)
        {
            return Results.BadRequest(new { error = exception.Message });
        }
    });

app.MapGet(
    "/api/worlds/{worldId:guid}/simulation",
    async (Guid worldId, IWorldSimulationControlService service, CancellationToken cancellationToken) =>
    {
        try { return Results.Ok(await service.GetStatusAsync(worldId, cancellationToken)); }
        catch (KeyNotFoundException) { return Results.NotFound(); }
    });

app.MapPost(
    "/api/worlds/{worldId:guid}/simulation/start",
    async (Guid worldId, IWorldSimulationControlService service, CancellationToken cancellationToken) =>
    {
        try { return Results.Ok(await service.StartAsync(worldId, cancellationToken)); }
        catch (KeyNotFoundException) { return Results.NotFound(); }
    });

app.MapPost(
    "/api/worlds/{worldId:guid}/simulation/pause",
    async (Guid worldId, IWorldSimulationControlService service, CancellationToken cancellationToken) =>
    {
        try { return Results.Ok(await service.PauseAsync(worldId, cancellationToken)); }
        catch (KeyNotFoundException) { return Results.NotFound(); }
    });

app.MapPost(
    "/api/worlds/{worldId:guid}/map/paint",
    async (Guid worldId, MapPaintApiRequest body, IMapEditingService service, CancellationToken cancellationToken) =>
    {
        if (!Enum.TryParse<MapBrushKind>(body.Brush, ignoreCase: true, out var brush))
            return Results.BadRequest(new { error = "Unknown map brush." });
        try
        {
            return Results.Ok(await service.PaintAsync(
                worldId,
                new MapPaintRequest(brush, body.CenterX, body.CenterY, body.Size),
                cancellationToken));
        }
        catch (KeyNotFoundException) { return Results.NotFound(); }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            return Results.BadRequest(new { error = exception.Message });
        }
    });

app.MapPost(
    "/api/worlds/{worldId:guid}/map/undo",
    async (Guid worldId, IMapEditingService service, CancellationToken cancellationToken) =>
        await service.UndoAsync(worldId, cancellationToken) is { } result
            ? Results.Ok(result)
            : Results.Conflict(new { error = "There is no map edit to undo." }));

app.MapPost(
    "/api/worlds/{worldId:guid}/map/redo",
    async (Guid worldId, IMapEditingService service, CancellationToken cancellationToken) =>
        await service.RedoAsync(worldId, cancellationToken) is { } result
            ? Results.Ok(result)
            : Results.Conflict(new { error = "There is no map edit to redo." }));

app.MapGet(
    "/api/worlds/{worldId:guid}/clock",
    async (Guid worldId, IWorldClockService service, CancellationToken cancellationToken) =>
    {
        try { return Results.Ok(await service.GetAsync(worldId, cancellationToken)); }
        catch (KeyNotFoundException) { return Results.NotFound(); }
    });

app.MapPost(
    "/api/worlds/{worldId:guid}/clock/ticks/{tickCount:int}",
    async (Guid worldId, int tickCount, IWorldClockService service, CancellationToken cancellationToken) =>
    {
        try { return Results.Ok(await service.AdvanceTicksAsync(worldId, tickCount, cancellationToken)); }
        catch (KeyNotFoundException) { return Results.NotFound(); }
        catch (ArgumentOutOfRangeException exception)
        { return Results.BadRequest(new { error = exception.Message }); }
    });

app.MapPut(
    "/api/worlds/{worldId:guid}/clock/configuration",
    async (Guid worldId, WorldClockConfigurationRequest body, IWorldClockService service, CancellationToken cancellationToken) =>
    {
        try
        {
            return Results.Ok(await service.ConfigureAsync(
                worldId,
                TimeSpan.FromSeconds(body.TickDurationSeconds),
                body.RealTimeMultiplier,
                cancellationToken));
        }
        catch (KeyNotFoundException) { return Results.NotFound(); }
        catch (ArgumentOutOfRangeException exception)
        { return Results.BadRequest(new { error = exception.Message }); }
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

static async Task<IResult> ImportWorldAsync(
    HttpRequest request,
    IWorldImportService importer,
    CancellationToken cancellationToken)
{
    if (!request.HasFormContentType)
    {
        return Results.BadRequest(new { error = "A multipart form with an image file is required." });
    }

    try
    {
        var form = await request.ReadFormAsync(cancellationToken);
        var file = form.Files.GetFile("file");

        if (file is null)
        {
            return Results.BadRequest(new { error = "The 'file' field is required." });
        }

        if (file.Length is <= 0 or > WorldImportService.MaximumFileSize)
        {
            return Results.BadRequest(new { error = "Image size must be between 1 byte and 10 MB." });
        }

        var name = form["name"].ToString();
        var gridResolution = int.TryParse(form["gridResolution"], out var parsedGrid)
            ? parsedGrid
            : 32;
        await using var stream = new MemoryStream((int)file.Length);
        await file.CopyToAsync(stream, cancellationToken);
        var result = await importer.ImportAsync(
            new WorldImportRequest(name, file.FileName, stream.ToArray(), gridResolution),
            cancellationToken);

        return Results.Created($"/api/worlds/{result.WorldId}/map", result);
    }
    catch (WorldImportValidationException exception)
    {
        return Results.BadRequest(new { error = exception.Message });
    }
    catch (InvalidDataException exception)
    {
        return Results.BadRequest(new { error = exception.Message });
    }
}

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}

record ManualBiomeRequest(string BiomeCode);

record MapPaintApiRequest(string Brush, int CenterX, int CenterY, int Size);

record WorldClockConfigurationRequest(double TickDurationSeconds, decimal RealTimeMultiplier);

public partial class Program
{
}

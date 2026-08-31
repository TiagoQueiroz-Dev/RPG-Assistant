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
using RpgWorld.Api.Authorization;
using RpgWorld.Application.Actors.Movement;
using RpgWorld.Domain.Actors.Traits;
using RpgWorld.Modules.Default.Actors;
using RpgWorld.Application.Actors.Inspection;
using RpgWorld.Application.Worlds.Cities;
using RpgWorld.Simulation.Worlds.Economy;
using RpgWorld.Application.Worlds.Factions;
using RpgWorld.Domain.Worlds.Factions;
using RpgWorld.Simulation.Worlds.Factions;
using RpgWorld.Application.Worlds.Events;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<IWorldDefinitionCatalog>(DefaultWorldDefinitions.Catalog);
builder.Services.AddSingleton<ITraitDefinitionCatalog>(DefaultActorDefinitions.Catalog);
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
    },
    cityEconomyOptions: builder.Configuration.GetSection("CityEconomy").Get<CityEconomyOptions>(),
    warDeclarationOptions: builder.Configuration.GetSection("WarDeclaration").Get<WarDeclarationOptions>());
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

app.MapPost(
    "/api/actors/{actorId:guid}/move",
    async (Guid actorId, ActorMoveApiRequest body, IActorMovementService service, CancellationToken cancellationToken) =>
    {
        try
        {
            return Results.Ok(await service.MoveAsync(
                new ActorMoveRequest(actorId, body.DestinationX, body.DestinationY),
                cancellationToken));
        }
        catch (KeyNotFoundException) { return Results.NotFound(); }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        { return Results.BadRequest(new { error = exception.Message }); }
    });

app.MapGet(
    "/api/worlds/{worldId:guid}/actors",
    async (Guid worldId, int x, int y, INpcInspectorService service, CancellationToken cancellationToken) =>
    {
        try { return Results.Ok(await service.ListAtPositionAsync(worldId, x, y, cancellationToken)); }
        catch (ArgumentOutOfRangeException exception)
        { return Results.BadRequest(new { error = exception.Message }); }
    });

app.MapGet(
    "/api/actors/{actorId:guid}/inspector",
    async (Guid actorId, INpcInspectorService service, CancellationToken cancellationToken) =>
        await service.GetNpcAsync(actorId, cancellationToken) is { } npc
            ? Results.Ok(npc)
            : Results.NotFound());

app.MapGet(
    "/api/worlds/{worldId:guid}/cities",
    async (Guid worldId, ICityService service, CancellationToken cancellationToken) =>
        Results.Ok(await service.ListByWorldAsync(worldId, cancellationToken)));

app.MapGet(
    "/api/cities/{cityId:guid}",
    async (Guid cityId, ICityService service, CancellationToken cancellationToken) =>
        await service.GetAsync(cityId, cancellationToken) is { } city
            ? Results.Ok(city)
            : Results.NotFound());

app.MapPost(
    "/api/worlds/{worldId:guid}/cities",
    async (HttpContext httpContext, Guid worldId, CreateCityApiRequest body, ICityService service, CancellationToken cancellationToken) =>
    {
        if (!GameMasterWorldAuthorization.HasContext(httpContext.User, worldId)) return Results.StatusCode(403);
        if (body.Territory is null) return Results.BadRequest(new { error = "City territory is required." });
        try
        {
            var created = await service.CreateAsync(new CreateCityRequest(
                worldId,
                body.Name,
                body.CenterX,
                body.CenterY,
                body.Territory.Select(cell => new CityTerritoryPosition(cell.X, cell.Y)).ToArray(),
                body.InitialPopulation,
                body.InitialWealth,
                body.FoundedAtUtc,
                body.GoverningFactionId,
                body.ResidentActorIds), cancellationToken);
            return Results.Created($"/api/cities/{created.CityId}", created);
        }
        catch (KeyNotFoundException) { return Results.NotFound(); }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        { return Results.BadRequest(new { error = exception.Message }); }
    });

app.MapGet(
    "/api/worlds/{worldId:guid}/factions",
    async (Guid worldId, IFactionService service, CancellationToken cancellationToken) =>
        Results.Ok(await service.ListByWorldAsync(worldId, cancellationToken)));

app.MapGet(
    "/api/worlds/{worldId:guid}/events",
    async (HttpContext httpContext, Guid worldId, int? page, int? pageSize, string? type,
        Guid? actorId, DateTimeOffset? fromUtc, DateTimeOffset? toUtc, int? x, int? y, Guid? correlationId,
        string? sort, IWorldEventService service, CancellationToken cancellationToken) =>
    {
        if (!GameMasterWorldAuthorization.HasContext(httpContext.User, worldId)) return Results.StatusCode(403);
        if (!Enum.TryParse<WorldEventSortOrder>(sort ?? nameof(WorldEventSortOrder.NewestFirst), true, out var sortOrder))
            return Results.BadRequest(new { error = "Unknown timeline sort order." });
        try
        {
            return Results.Ok(await service.SearchAsync(new WorldEventQuery(
                worldId, page ?? 1, pageSize ?? 50, type, actorId, fromUtc, toUtc, x, y, sortOrder, correlationId),
                cancellationToken));
        }
        catch (KeyNotFoundException) { return Results.NotFound(); }
        catch (Exception exception) when (exception is ArgumentException or OverflowException)
        { return Results.BadRequest(new { error = exception.Message }); }
    });

app.MapGet(
    "/api/factions/{factionId:guid}",
    async (Guid factionId, IFactionService service, CancellationToken cancellationToken) =>
        await service.GetAsync(factionId, cancellationToken) is { } faction
            ? Results.Ok(faction)
            : Results.NotFound());

app.MapPost(
    "/api/worlds/{worldId:guid}/factions",
    async (HttpContext httpContext, Guid worldId, CreateFactionApiRequest body, IFactionService service, CancellationToken cancellationToken) =>
    {
        if (!GameMasterWorldAuthorization.HasContext(httpContext.User, worldId)) return Results.StatusCode(403);
        if (!Enum.TryParse<FactionType>(body.Type, ignoreCase: true, out var type))
            return Results.BadRequest(new { error = "Unknown faction type." });
        try
        {
            var created = await service.CreateAsync(new CreateFactionRequest(
                worldId,
                body.Name,
                type,
                body.LeaderActorId,
                body.InitialWealth,
                body.InitialMilitaryPower,
                body.CreatedAtUtc,
                body.Territory?.Select(cell => new FactionTerritoryPosition(cell.X, cell.Y)).ToArray()),
                cancellationToken);
            return Results.Created($"/api/factions/{created.FactionId}", created);
        }
        catch (KeyNotFoundException) { return Results.NotFound(); }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        { return Results.BadRequest(new { error = exception.Message }); }
    });

app.MapPost(
    "/api/worlds/{worldId:guid}/factions/{factionId:guid}/members/{actorId:guid}",
    async (HttpContext httpContext, Guid worldId, Guid factionId, Guid actorId, FactionOccurredAtApiRequest body, IFactionService service, CancellationToken cancellationToken) =>
    {
        if (!GameMasterWorldAuthorization.HasContext(httpContext.User, worldId)) return Results.StatusCode(403);
        try
        {
            if (await service.GetAsync(factionId, cancellationToken) is not { } faction) return Results.NotFound();
            if (faction.WorldId != worldId) return Results.BadRequest(new { error = "Faction does not belong to this world." });
            return Results.Ok(await service.AddMemberAsync(factionId, actorId, body.OccurredAtUtc, cancellationToken));
        }
        catch (KeyNotFoundException) { return Results.NotFound(); }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        { return Results.BadRequest(new { error = exception.Message }); }
    });

app.MapPost(
    "/api/worlds/{worldId:guid}/factions/{factionId:guid}/members/{actorId:guid}/remove",
    async (HttpContext httpContext, Guid worldId, Guid factionId, Guid actorId, FactionReasonApiRequest body, IFactionService service, CancellationToken cancellationToken) =>
    {
        if (!GameMasterWorldAuthorization.HasContext(httpContext.User, worldId)) return Results.StatusCode(403);
        try
        {
            if (await service.GetAsync(factionId, cancellationToken) is not { } faction) return Results.NotFound();
            if (faction.WorldId != worldId) return Results.BadRequest(new { error = "Faction does not belong to this world." });
            return Results.Ok(await service.RemoveMemberAsync(
                factionId, actorId, body.Reason, body.OccurredAtUtc, cancellationToken));
        }
        catch (KeyNotFoundException) { return Results.NotFound(); }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        { return Results.BadRequest(new { error = exception.Message }); }
    });

app.MapPut(
    "/api/worlds/{worldId:guid}/factions/{factionId:guid}/leader",
    async (HttpContext httpContext, Guid worldId, Guid factionId, ChangeFactionLeaderApiRequest body, IFactionService service, CancellationToken cancellationToken) =>
    {
        if (!GameMasterWorldAuthorization.HasContext(httpContext.User, worldId)) return Results.StatusCode(403);
        try
        {
            if (await service.GetAsync(factionId, cancellationToken) is not { } faction) return Results.NotFound();
            if (faction.WorldId != worldId) return Results.BadRequest(new { error = "Faction does not belong to this world." });
            return Results.Ok(await service.ChangeLeaderAsync(
                factionId, body.NewLeaderActorId, body.Reason, body.OccurredAtUtc, cancellationToken));
        }
        catch (KeyNotFoundException) { return Results.NotFound(); }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        { return Results.BadRequest(new { error = exception.Message }); }
    });

app.MapPost(
    "/api/worlds/{worldId:guid}/factions/{factionId:guid}/cities/{cityId:guid}",
    async (HttpContext httpContext, Guid worldId, Guid factionId, Guid cityId, AssociateFactionCityApiRequest body, IFactionService service, CancellationToken cancellationToken) =>
    {
        if (!GameMasterWorldAuthorization.HasContext(httpContext.User, worldId)) return Results.StatusCode(403);
        try
        {
            if (await service.GetAsync(factionId, cancellationToken) is not { } faction) return Results.NotFound();
            if (faction.WorldId != worldId) return Results.BadRequest(new { error = "Faction does not belong to this world." });
            return Results.Ok(await service.AssociateCityAsync(
                factionId, cityId, body.ClaimCityTerritory, body.OccurredAtUtc, cancellationToken));
        }
        catch (KeyNotFoundException) { return Results.NotFound(); }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        { return Results.BadRequest(new { error = exception.Message }); }
    });

app.MapPost(
    "/api/worlds/{worldId:guid}/factions/{factionId:guid}/wealth",
    async (HttpContext httpContext, Guid worldId, Guid factionId, AdjustFactionWealthApiRequest body, IFactionService service, CancellationToken cancellationToken) =>
    {
        if (!GameMasterWorldAuthorization.HasContext(httpContext.User, worldId)) return Results.StatusCode(403);
        try
        {
            if (await service.GetAsync(factionId, cancellationToken) is not { } faction) return Results.NotFound();
            if (faction.WorldId != worldId) return Results.BadRequest(new { error = "Faction does not belong to this world." });
            return Results.Ok(await service.AdjustWealthAsync(
                factionId, body.Delta, body.Reason, body.OccurredAtUtc, cancellationToken));
        }
        catch (KeyNotFoundException) { return Results.NotFound(); }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or OverflowException)
        { return Results.BadRequest(new { error = exception.Message }); }
    });

app.MapPut(
    "/api/worlds/{worldId:guid}/factions/{factionId:guid}/military-power",
    async (HttpContext httpContext, Guid worldId, Guid factionId, SetFactionPowerApiRequest body, IFactionService service, CancellationToken cancellationToken) =>
    {
        if (!GameMasterWorldAuthorization.HasContext(httpContext.User, worldId)) return Results.StatusCode(403);
        try
        {
            if (await service.GetAsync(factionId, cancellationToken) is not { } faction) return Results.NotFound();
            if (faction.WorldId != worldId) return Results.BadRequest(new { error = "Faction does not belong to this world." });
            return Results.Ok(await service.SetMilitaryPowerAsync(
                factionId, body.Value, body.Reason, body.OccurredAtUtc, cancellationToken));
        }
        catch (KeyNotFoundException) { return Results.NotFound(); }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        { return Results.BadRequest(new { error = exception.Message }); }
    });

app.MapPost(
    "/api/worlds/{worldId:guid}/factions/{factionId:guid}/relations/{targetFactionId:guid}/modifiers",
    async (HttpContext httpContext, Guid worldId, Guid factionId, Guid targetFactionId, FactionRelationModifierApiRequest body, IFactionService service, CancellationToken cancellationToken) =>
    {
        if (!GameMasterWorldAuthorization.HasContext(httpContext.User, worldId)) return Results.StatusCode(403);
        if (!Enum.TryParse<FactionRelationModifierSource>(body.Source, ignoreCase: true, out var source))
            return Results.BadRequest(new { error = "Unknown diplomatic modifier source." });
        try
        {
            if (await service.GetAsync(factionId, cancellationToken) is not { } faction) return Results.NotFound();
            if (faction.WorldId != worldId) return Results.BadRequest(new { error = "Faction does not belong to this world." });
            var modifier = new FactionRelationModifier(
                source,
                body.Reason,
                body.AffinityDelta,
                body.TensionDelta,
                body.SourceEventId,
                body.Vassalage);
            return Results.Ok(await service.ApplyRelationModifierAsync(
                factionId, targetFactionId, modifier, body.OccurredAtUtc, cancellationToken));
        }
        catch (KeyNotFoundException) { return Results.NotFound(); }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or OverflowException)
        { return Results.BadRequest(new { error = exception.Message }); }
    });

app.MapPost(
    "/api/worlds/{worldId:guid}/factions/{factionId:guid}/dissolve",
    async (HttpContext httpContext, Guid worldId, Guid factionId, FactionReasonApiRequest body, IFactionService service, CancellationToken cancellationToken) =>
    {
        if (!GameMasterWorldAuthorization.HasContext(httpContext.User, worldId)) return Results.StatusCode(403);
        try
        {
            if (await service.GetAsync(factionId, cancellationToken) is not { } faction) return Results.NotFound();
            if (faction.WorldId != worldId) return Results.BadRequest(new { error = "Faction does not belong to this world." });
            return Results.Ok(await service.DissolveAsync(
                factionId, body.Reason, body.OccurredAtUtc, cancellationToken));
        }
        catch (KeyNotFoundException) { return Results.NotFound(); }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        { return Results.BadRequest(new { error = exception.Message }); }
    });

app.MapPost(
    "/api/worlds/{worldId:guid}/factions/{factionId:guid}/wars/{targetFactionId:guid}/force",
    async (HttpContext httpContext, Guid worldId, Guid factionId, Guid targetFactionId, FactionReasonApiRequest body, IFactionService service, CancellationToken cancellationToken) =>
    {
        if (!GameMasterWorldAuthorization.HasContext(httpContext.User, worldId)) return Results.StatusCode(403);
        try
        {
            if (await service.GetAsync(factionId, cancellationToken) is not { } faction) return Results.NotFound();
            if (faction.WorldId != worldId) return Results.BadRequest(new { error = "Faction does not belong to this world." });
            return Results.Ok(await service.ForceWarAsync(factionId, targetFactionId, body.Reason, body.OccurredAtUtc, cancellationToken));
        }
        catch (KeyNotFoundException) { return Results.NotFound(); }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        { return Results.BadRequest(new { error = exception.Message }); }
    });

app.MapPost(
    "/api/worlds/{worldId:guid}/factions/{factionId:guid}/wars/{targetFactionId:guid}/prevent",
    async (HttpContext httpContext, Guid worldId, Guid factionId, Guid targetFactionId, PreventFactionWarApiRequest body, IFactionService service, CancellationToken cancellationToken) =>
    {
        if (!GameMasterWorldAuthorization.HasContext(httpContext.User, worldId)) return Results.StatusCode(403);
        try
        {
            if (await service.GetAsync(factionId, cancellationToken) is not { } faction) return Results.NotFound();
            if (faction.WorldId != worldId) return Results.BadRequest(new { error = "Faction does not belong to this world." });
            return Results.Ok(await service.PreventWarAsync(factionId, targetFactionId, body.PreventedUntilUtc, body.Reason, body.OccurredAtUtc, cancellationToken));
        }
        catch (KeyNotFoundException) { return Results.NotFound(); }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        { return Results.BadRequest(new { error = exception.Message }); }
    });

app.MapPost(
    "/api/worlds/{worldId:guid}/factions/{factionId:guid}/wars/{targetFactionId:guid}/allow",
    async (HttpContext httpContext, Guid worldId, Guid factionId, Guid targetFactionId, FactionReasonApiRequest body, IFactionService service, CancellationToken cancellationToken) =>
    {
        if (!GameMasterWorldAuthorization.HasContext(httpContext.User, worldId)) return Results.StatusCode(403);
        try
        {
            if (await service.GetAsync(factionId, cancellationToken) is not { } faction) return Results.NotFound();
            if (faction.WorldId != worldId) return Results.BadRequest(new { error = "Faction does not belong to this world." });
            return Results.Ok(await service.AllowWarAsync(factionId, targetFactionId, body.Reason, body.OccurredAtUtc, cancellationToken));
        }
        catch (KeyNotFoundException) { return Results.NotFound(); }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        { return Results.BadRequest(new { error = exception.Message }); }
    });

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
    async (HttpContext httpContext, Guid worldId, IWorldTimeCommandService service, CancellationToken cancellationToken) =>
    {
        if (!GameMasterWorldAuthorization.HasContext(httpContext.User, worldId)) return Results.StatusCode(403);
        try { return Results.Ok(await service.ResumeAsync(worldId, cancellationToken)); }
        catch (KeyNotFoundException) { return Results.NotFound(); }
    });

app.MapPost(
    "/api/worlds/{worldId:guid}/simulation/pause",
    async (HttpContext httpContext, Guid worldId, IWorldTimeCommandService service, CancellationToken cancellationToken) =>
    {
        if (!GameMasterWorldAuthorization.HasContext(httpContext.User, worldId)) return Results.StatusCode(403);
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
    async (HttpContext httpContext, Guid worldId, int tickCount, IWorldTimeCommandService service, CancellationToken cancellationToken) =>
    {
        if (!GameMasterWorldAuthorization.HasContext(httpContext.User, worldId)) return Results.StatusCode(403);
        try
        {
            return Results.Ok(await service.AdvanceTicksAsync(worldId, tickCount, cancellationToken));
        }
        catch (KeyNotFoundException) { return Results.NotFound(); }
        catch (Exception exception) when (exception is ArgumentOutOfRangeException or OverflowException)
        { return Results.BadRequest(new { error = exception.Message }); }
    });

app.MapPut(
    "/api/worlds/{worldId:guid}/clock/configuration",
    async (HttpContext httpContext, Guid worldId, WorldClockConfigurationRequest body, IWorldTimeCommandService service, CancellationToken cancellationToken) =>
    {
        if (!GameMasterWorldAuthorization.HasContext(httpContext.User, worldId)) return Results.StatusCode(403);
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

app.MapPost(
    "/api/worlds/{worldId:guid}/time/pause",
    async (HttpContext httpContext, Guid worldId, IWorldTimeCommandService service, CancellationToken cancellationToken) =>
    {
        if (!GameMasterWorldAuthorization.HasContext(httpContext.User, worldId)) return Results.StatusCode(403);
        try { return Results.Ok(await service.PauseAsync(worldId, cancellationToken)); }
        catch (KeyNotFoundException) { return Results.NotFound(); }
    });

app.MapPost(
    "/api/worlds/{worldId:guid}/time/resume",
    async (HttpContext httpContext, Guid worldId, IWorldTimeCommandService service, CancellationToken cancellationToken) =>
    {
        if (!GameMasterWorldAuthorization.HasContext(httpContext.User, worldId)) return Results.StatusCode(403);
        try { return Results.Ok(await service.ResumeAsync(worldId, cancellationToken)); }
        catch (KeyNotFoundException) { return Results.NotFound(); }
    });

app.MapPut(
    "/api/worlds/{worldId:guid}/time/speed",
    async (HttpContext httpContext, Guid worldId, WorldTimeSpeedRequest body, IWorldTimeCommandService service, CancellationToken cancellationToken) =>
    {
        if (!GameMasterWorldAuthorization.HasContext(httpContext.User, worldId)) return Results.StatusCode(403);
        try { return Results.Ok(await service.SetMultiplierAsync(worldId, body.RealTimeMultiplier, cancellationToken)); }
        catch (KeyNotFoundException) { return Results.NotFound(); }
        catch (ArgumentOutOfRangeException exception) { return Results.BadRequest(new { error = exception.Message }); }
    });

app.MapPost(
    "/api/worlds/{worldId:guid}/time/advance",
    async (HttpContext httpContext, Guid worldId, WorldTimeAdvanceRequest body, IWorldTimeCommandService service, CancellationToken cancellationToken) =>
    {
        if (!GameMasterWorldAuthorization.HasContext(httpContext.User, worldId)) return Results.StatusCode(403);
        try { return Results.Ok(await service.AdvanceAsync(worldId, TimeSpan.FromSeconds(body.DurationSeconds), cancellationToken)); }
        catch (KeyNotFoundException) { return Results.NotFound(); }
        catch (Exception exception) when (exception is ArgumentException or OverflowException)
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

record WorldTimeSpeedRequest(decimal RealTimeMultiplier);

record WorldTimeAdvanceRequest(double DurationSeconds);

record ActorMoveApiRequest(int DestinationX, int DestinationY);

record CityTerritoryApiPosition(int X, int Y);

record CreateCityApiRequest(
    string Name,
    int CenterX,
    int CenterY,
    CityTerritoryApiPosition[]? Territory,
    int InitialPopulation,
    decimal InitialWealth,
    DateTimeOffset FoundedAtUtc,
    Guid? GoverningFactionId,
    Guid[]? ResidentActorIds);

record FactionTerritoryApiPosition(int X, int Y);

record CreateFactionApiRequest(
    string Name,
    string Type,
    Guid LeaderActorId,
    decimal InitialWealth,
    decimal InitialMilitaryPower,
    DateTimeOffset CreatedAtUtc,
    FactionTerritoryApiPosition[]? Territory);

record FactionOccurredAtApiRequest(DateTimeOffset OccurredAtUtc);
record FactionReasonApiRequest(string Reason, DateTimeOffset OccurredAtUtc);
record PreventFactionWarApiRequest(DateTimeOffset PreventedUntilUtc, string Reason, DateTimeOffset OccurredAtUtc);
record ChangeFactionLeaderApiRequest(Guid NewLeaderActorId, string Reason, DateTimeOffset OccurredAtUtc);
record AssociateFactionCityApiRequest(bool ClaimCityTerritory, DateTimeOffset OccurredAtUtc);
record AdjustFactionWealthApiRequest(decimal Delta, string Reason, DateTimeOffset OccurredAtUtc);
record SetFactionPowerApiRequest(decimal Value, string Reason, DateTimeOffset OccurredAtUtc);
record FactionRelationModifierApiRequest(
    string Source,
    string Reason,
    int AffinityDelta,
    int TensionDelta,
    Guid? SourceEventId,
    bool? Vassalage,
    DateTimeOffset OccurredAtUtc);

public partial class Program
{
}

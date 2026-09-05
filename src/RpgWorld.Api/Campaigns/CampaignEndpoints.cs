using System.Text.Json;
using RpgWorld.Api.Authorization;
using RpgWorld.Application.Campaigns;

namespace RpgWorld.Api.Campaigns;

public static class CampaignEndpoints
{
    public static void MapCampaignEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/worlds/{worldId:guid}/campaigns");
        group.AddEndpointFilter(async (context, next) =>
        {
            var worldId = Guid.Parse(context.HttpContext.Request.RouteValues["worldId"]!.ToString()!);
            if (!GameMasterWorldAuthorization.HasContext(context.HttpContext.User, worldId))
                return Results.StatusCode(403);
            try { return await next(context); }
            catch (KeyNotFoundException) { return Results.NotFound(); }
            catch (Exception exception) when (exception is ArgumentException or JsonException)
            { return Results.BadRequest(new { error = exception.Message }); }
        });
        group.MapPost("", async (Guid worldId, CreateCampaignBody body, ICampaignService service, CancellationToken token) =>
        {
            var campaign = await service.CreateAsync(worldId, new CreateCampaignRequest(body.Name, body.ModuleId,
                body.Settings.ValueKind == JsonValueKind.Undefined ? "{}" : body.Settings.GetRawText()), token);
            return Results.Created($"/api/worlds/{worldId}/campaigns/{campaign.Id}", campaign);
        });
        group.MapGet("", async (Guid worldId, ICampaignService service, CancellationToken token,
            int offset = 0, int limit = 50) => Results.Ok(await service.ListAsync(worldId, offset, limit, token)));
        group.MapGet("/{campaignId:guid}", async (Guid worldId, Guid campaignId, ICampaignService service,
            CancellationToken token) => Results.Ok(await service.GetAsync(worldId, campaignId, token)));
        group.MapPost("/{campaignId:guid}/end", async (Guid worldId, Guid campaignId, ICampaignService service,
            CancellationToken token) => Results.Ok(await service.EndAsync(worldId, campaignId, token)));
    }

    public sealed record CreateCampaignBody(string Name, string ModuleId, JsonElement Settings);
}

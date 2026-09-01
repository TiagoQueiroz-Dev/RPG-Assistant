using Microsoft.EntityFrameworkCore;
using RpgWorld.Application.Worlds.Visibility;
using RpgWorld.Domain.Actors;
using RpgWorld.Domain.Worlds;
using RpgWorld.Infrastructure.Persistence;

namespace RpgWorld.Infrastructure.Worlds.Visibility;

public sealed class PlayerVisibilityService(
    RpgWorldDbContext dbContext,
    TimeProvider timeProvider) : IPlayerVisibilityService
{
    public const int DefaultPerceptionRadius = 2;
    public const int MaximumPerceptionRadius = 12;

    public async Task<PlayerVisibilityView> GetAsync(
        Guid playerActorId,
        CancellationToken cancellationToken = default)
    {
        var player = await RequiredPlayerAsync(playerActorId, cancellationToken);
        var observedAt = timeProvider.GetUtcNow();
        var latestKnown = await dbContext.PlayerTileKnowledge.Where(value => value.PlayerActorId == player.Id)
            .MaxAsync(value => (DateTimeOffset?)value.LastVisibleAtUtc, cancellationToken);
        if (latestKnown is { } latest && observedAt < latest) observedAt = latest;
        await RefreshPlayerAsync(player, observedAt, cancellationToken);
        var radius = PerceptionRadius(player);
        var knowledge = await dbContext.PlayerTileKnowledge.AsNoTracking()
            .Where(value => value.PlayerActorId == player.Id)
            .OrderBy(value => value.Y).ThenBy(value => value.X)
            .ToArrayAsync(cancellationToken);
        return new PlayerVisibilityView(
            player.Id,
            player.WorldId,
            player.X,
            player.Y,
            radius,
            knowledge.Select(value => new PlayerTileVisibility(
                value.X,
                value.Y,
                value.CurrentState(IsVisible(player.X, player.Y, value.X, value.Y, radius)).ToString())).ToArray());
    }

    public async Task RefreshAsync(
        Guid playerActorId,
        DateTimeOffset observedAtUtc,
        CancellationToken cancellationToken = default) =>
        await RefreshPlayerAsync(
            await RequiredPlayerAsync(playerActorId, cancellationToken),
            observedAtUtc,
            cancellationToken);

    public async Task<IReadOnlyList<Guid>> ListPlayersSeeingAsync(
        Guid worldId,
        int x,
        int y,
        CancellationToken cancellationToken = default)
    {
        if (worldId == Guid.Empty) throw new ArgumentException("World identifier is required.", nameof(worldId));
        var players = await dbContext.Actors.AsNoTracking().OfType<PlayerActor>()
            .Where(value => value.WorldId == worldId && value.Status != ActorStatus.Dead)
            .ToArrayAsync(cancellationToken);
        return players.Where(player => IsVisible(player.X, player.Y, x, y, PerceptionRadius(player)))
            .Select(player => player.Id).ToArray();
    }

    private async Task RefreshPlayerAsync(
        PlayerActor player,
        DateTimeOffset observedAtUtc,
        CancellationToken cancellationToken)
    {
        var radius = PerceptionRadius(player);
        var minX = Math.Max(0, player.X - radius);
        var minY = Math.Max(0, player.Y - radius);
        var maxX = player.X + radius;
        var maxY = player.Y + radius;
        var tiles = await dbContext.Tiles.AsNoTracking().Where(value => value.WorldId == player.WorldId &&
            value.X >= minX && value.X <= maxX && value.Y >= minY && value.Y <= maxY)
            .Select(value => new { value.X, value.Y }).ToArrayAsync(cancellationToken);
        var existing = await dbContext.PlayerTileKnowledge.Where(value => value.PlayerActorId == player.Id &&
            value.X >= minX && value.X <= maxX && value.Y >= minY && value.Y <= maxY)
            .ToDictionaryAsync(value => (value.X, value.Y), cancellationToken);
        foreach (var tile in tiles)
        {
            var known = tile.X == player.X && tile.Y == player.Y;
            if (existing.TryGetValue((tile.X, tile.Y), out var knowledge))
                knowledge.Observe(known, observedAtUtc);
            else
                dbContext.PlayerTileKnowledge.Add(PlayerTileKnowledge.Discover(
                    player.Id, new Position(player.WorldId, tile.X, tile.Y), known, observedAtUtc));
        }
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<PlayerActor> RequiredPlayerAsync(Guid playerActorId, CancellationToken cancellationToken)
    {
        if (playerActorId == Guid.Empty) throw new ArgumentException("Player actor identifier is required.", nameof(playerActorId));
        return await dbContext.Actors.OfType<PlayerActor>().SingleOrDefaultAsync(value => value.Id == playerActorId, cancellationToken)
            ?? throw new KeyNotFoundException($"Player actor '{playerActorId}' was not found.");
    }

    private static int PerceptionRadius(PlayerActor player) =>
        Math.Clamp(player.Attributes.GetValueOrDefault("perception", DefaultPerceptionRadius), 1, MaximumPerceptionRadius);

    private static bool IsVisible(int playerX, int playerY, int x, int y, int radius) =>
        Math.Max(Math.Abs(playerX - x), Math.Abs(playerY - y)) <= radius;
}

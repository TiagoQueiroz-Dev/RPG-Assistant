using System.Globalization;
using RpgWorld.Application.Realtime;
using RpgWorld.Application.Worlds;
using RpgWorld.Simulation.Engine;

namespace RpgWorld.Simulation.Time;

public sealed class WorldTimeCommandService(
    IWorldSimulationRepository worldRepository,
    IWorldClockService clockService,
    IWorldCommandGate commandGate,
    ISimulationScheduler scheduler,
    ISimulationSystemRunner systemRunner,
    IWorldUpdatePublisher publisher,
    TimeProvider timeProvider) : IWorldTimeCommandService
{
    public Task<WorldTimeCommandResult> PauseAsync(
        Guid worldId,
        CancellationToken cancellationToken = default) =>
        ExecuteAsync(worldId, "paused", async token =>
        {
            var world = await GetWorldAsync(worldId, token);
            var clock = world.IsSimulationRunning
                ? await clockService.SynchronizeAsync(worldId, token)
                : await clockService.GetAsync(worldId, token);
            world.PauseSimulation();
            await worldRepository.SaveChangesAsync(token);
            return clock;
        }, cancellationToken);

    public Task<WorldTimeCommandResult> ResumeAsync(
        Guid worldId,
        CancellationToken cancellationToken = default) =>
        ExecuteAsync(worldId, "resumed", async token =>
        {
            var world = await GetWorldAsync(worldId, token);
            var clock = world.IsSimulationRunning
                ? await clockService.GetAsync(worldId, token)
                : await clockService.RebaseAsync(worldId, token);
            world.StartSimulation();
            await worldRepository.SaveChangesAsync(token);
            return clock;
        }, cancellationToken);

    public Task<WorldTimeCommandResult> SetMultiplierAsync(
        Guid worldId,
        decimal multiplier,
        CancellationToken cancellationToken = default) =>
        ExecuteAsync(worldId, "speed-changed", async token =>
        {
            var world = await GetWorldAsync(worldId, token);
            var current = world.IsSimulationRunning
                ? await clockService.SynchronizeAsync(worldId, token)
                : await clockService.GetAsync(worldId, token);
            return await clockService.ConfigureAsync(worldId, current.TickDuration, multiplier, token);
        }, cancellationToken);

    public Task<WorldTimeCommandResult> ConfigureAsync(
        Guid worldId,
        TimeSpan tickDuration,
        decimal multiplier,
        CancellationToken cancellationToken = default) =>
        ExecuteAsync(worldId, "configured", async token =>
        {
            var world = await GetWorldAsync(worldId, token);
            if (world.IsSimulationRunning) await clockService.SynchronizeAsync(worldId, token);
            return await clockService.ConfigureAsync(worldId, tickDuration, multiplier, token);
        }, cancellationToken);

    public Task<WorldTimeCommandResult> AdvanceAsync(
        Guid worldId,
        TimeSpan duration,
        CancellationToken cancellationToken = default)
    {
        if (duration <= TimeSpan.Zero || duration > TimeSpan.FromDays(365))
            throw new ArgumentOutOfRangeException(nameof(duration), "Manual advance must be between one tick and 365 days.");
        return ExecuteAsync(worldId, "advanced", async token =>
        {
            await GetWorldAsync(worldId, token);
            var clock = await clockService.AdvanceByAsync(worldId, duration, token);
            scheduler.AdvanceWorld(worldId, duration);
            await systemRunner.RunAsync(new SimulationTickContext(worldId, clock), token);
            return clock;
        }, cancellationToken);
    }

    public Task<WorldTimeCommandResult> AdvanceTicksAsync(
        Guid worldId,
        int tickCount,
        CancellationToken cancellationToken = default)
    {
        if (tickCount <= 0) throw new ArgumentOutOfRangeException(nameof(tickCount));
        return ExecuteAsync(worldId, "advanced", async token =>
        {
            await GetWorldAsync(worldId, token);
            var current = await clockService.GetAsync(worldId, token);
            var duration = current.TickDuration * tickCount;
            if (duration > TimeSpan.FromDays(365))
                throw new ArgumentOutOfRangeException(nameof(tickCount), "Manual advance cannot exceed 365 days.");
            var clock = await clockService.AdvanceByAsync(worldId, duration, token);
            scheduler.AdvanceWorld(worldId, duration);
            await systemRunner.RunAsync(new SimulationTickContext(worldId, clock), token);
            return clock;
        }, cancellationToken);
    }

    private Task<WorldTimeCommandResult> ExecuteAsync(
        Guid worldId,
        string command,
        Func<CancellationToken, Task<WorldClockSnapshot>> operation,
        CancellationToken cancellationToken) =>
        commandGate.ExecuteAsync(worldId, async token =>
        {
            var clock = await operation(token);
            var world = await GetWorldAsync(worldId, token);
            var result = new WorldTimeCommandResult(
                worldId,
                world.IsSimulationRunning,
                clock.CurrentInstant,
                clock.TickDuration,
                clock.RealTimeMultiplier,
                command);
            await publisher.PublishToGameMasterAsync(new WorldUpdateMessage(
                Guid.CreateVersion7(),
                worldId,
                $"world.time.{command}",
                timeProvider.GetUtcNow(),
                new Dictionary<string, string?>
                {
                    ["isRunning"] = result.IsRunning.ToString(CultureInfo.InvariantCulture),
                    ["currentInstant"] = result.CurrentInstant.ToString("O", CultureInfo.InvariantCulture),
                    ["tickDuration"] = result.TickDuration.ToString("c", CultureInfo.InvariantCulture),
                    ["realTimeMultiplier"] = result.RealTimeMultiplier.ToString(CultureInfo.InvariantCulture)
                }), token);
            return result;
        }, cancellationToken);

    private async Task<RpgWorld.Domain.Worlds.World> GetWorldAsync(
        Guid worldId,
        CancellationToken cancellationToken) =>
        await worldRepository.GetAsync(worldId, cancellationToken)
        ?? throw new KeyNotFoundException("World was not found.");
}

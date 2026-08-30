namespace RpgWorld.Simulation.Time;

public interface IWorldTimeCommandService
{
    Task<WorldTimeCommandResult> PauseAsync(Guid worldId, CancellationToken cancellationToken = default);
    Task<WorldTimeCommandResult> ResumeAsync(Guid worldId, CancellationToken cancellationToken = default);
    Task<WorldTimeCommandResult> SetMultiplierAsync(Guid worldId, decimal multiplier, CancellationToken cancellationToken = default);
    Task<WorldTimeCommandResult> ConfigureAsync(Guid worldId, TimeSpan tickDuration, decimal multiplier, CancellationToken cancellationToken = default);
    Task<WorldTimeCommandResult> AdvanceTicksAsync(Guid worldId, int tickCount, CancellationToken cancellationToken = default);
    Task<WorldTimeCommandResult> AdvanceAsync(Guid worldId, TimeSpan duration, CancellationToken cancellationToken = default);
}

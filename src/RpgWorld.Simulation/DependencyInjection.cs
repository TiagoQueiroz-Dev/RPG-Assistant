using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RpgWorld.Simulation.Chunks;
using RpgWorld.Simulation.Time;

namespace RpgWorld.Simulation;

public static class DependencyInjection
{
    public static IServiceCollection AddSimulation(
        this IServiceCollection services,
        ChunkActivationOptions? chunkActivationOptions = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton(chunkActivationOptions ?? new ChunkActivationOptions());
        services.AddScoped<IChunkActivationService, ChunkActivationService>();
        services.AddScoped<IWorldClockService, WorldClockService>();
        return services;
    }
}

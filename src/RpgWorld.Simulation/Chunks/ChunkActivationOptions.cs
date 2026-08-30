namespace RpgWorld.Simulation.Chunks;

public sealed record ChunkActivationOptions
{
    public ChunkActivationOptions(
        int playerRadius = 1,
        TimeSpan? inactivityTimeout = null)
    {
        if (playerRadius is < 0 or > 16)
        {
            throw new ArgumentOutOfRangeException(
                nameof(playerRadius),
                "Player activation radius must be between 0 and 16 chunks.");
        }

        var effectiveTimeout = inactivityTimeout ?? TimeSpan.FromMinutes(5);

        if (effectiveTimeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(inactivityTimeout),
                "Chunk inactivity timeout must be positive.");
        }

        PlayerRadius = playerRadius;
        InactivityTimeout = effectiveTimeout;
    }

    public int PlayerRadius { get; }

    public TimeSpan InactivityTimeout { get; }
}

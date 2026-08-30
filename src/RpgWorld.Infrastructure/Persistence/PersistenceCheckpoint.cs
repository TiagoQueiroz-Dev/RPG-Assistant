using System.Text.Json;

namespace RpgWorld.Infrastructure.Persistence;

/// <summary>
/// Small durable record used to verify that the persistence pipeline is healthy.
/// Domain aggregates will be added to the same context by their respective features.
/// </summary>
public sealed class PersistenceCheckpoint
{
    private PersistenceCheckpoint()
    {
    }

    public PersistenceCheckpoint(
        Guid id,
        DateTimeOffset createdAtUtc,
        PersistenceCheckpointStatus status,
        JsonDocument metadata)
    {
        ArgumentNullException.ThrowIfNull(metadata);

        Id = id;
        CreatedAtUtc = createdAtUtc;
        Status = status;
        Metadata = metadata;
    }

    public Guid Id { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public PersistenceCheckpointStatus Status { get; private set; }

    public JsonDocument Metadata { get; private set; } = null!;
}

public enum PersistenceCheckpointStatus
{
    Pending,
    Succeeded,
    Failed
}


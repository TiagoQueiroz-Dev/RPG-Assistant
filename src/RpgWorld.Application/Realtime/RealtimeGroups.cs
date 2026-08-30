namespace RpgWorld.Application.Realtime;

public static class RealtimeGroups
{
    public static string World(Guid worldId) => $"world:{Required(worldId):N}";

    public static string Chunk(Guid chunkId) => $"chunk:{Required(chunkId):N}";

    public static string Player(Guid playerId) => $"player:{Required(playerId):N}";

    public static string GameMaster(Guid worldId) => $"gm:{Required(worldId):N}";

    private static Guid Required(Guid id) => id == Guid.Empty
        ? throw new ArgumentException("Group identifier cannot be empty.", nameof(id))
        : id;
}


namespace RpgWorld.Application.Worlds.Admin;

public enum GameMasterCommandType
{
    CreateNpc,
    DeleteNpc,
    MoveActor,
    CreateCity,
    DestroyCity,
    AdjustResource,
    CreateCreature,
    ChangeClimate,
    CreateEvent,
    DeclareWar,
    EndWar,
    ChangeFactionRelation
}

public sealed record GameMasterCommandPosition(int X, int Y);

public sealed record GameMasterCommand(
    GameMasterCommandType Type,
    DateTimeOffset? OccurredAtUtc = null,
    Guid? ActorId = null,
    Guid? CityId = null,
    Guid? ResourceDepositId = null,
    Guid? FactionId = null,
    Guid? TargetFactionId = null,
    string? Name = null,
    string? Reason = null,
    int? X = null,
    int? Y = null,
    int? MaximumHealth = null,
    int? InitialPopulation = null,
    decimal? InitialWealth = null,
    IReadOnlyList<GameMasterCommandPosition>? Territory = null,
    decimal? ResourceQuantityDelta = null,
    decimal? TemperatureCelsius = null,
    decimal? Humidity = null,
    string? EventType = null,
    string? EventPayload = null,
    int? AffinityDelta = null,
    int? TensionDelta = null,
    bool? Vassalage = null);

public sealed record GameMasterCommandResult(
    Guid CommandId,
    Guid WorldId,
    string Command,
    Guid? EntityId,
    DateTimeOffset OccurredAtUtc,
    string Summary);

public interface IGameMasterCommandService
{
    Task<GameMasterCommandResult> ExecuteAsync(
        Guid worldId,
        GameMasterCommand command,
        CancellationToken cancellationToken = default);
}

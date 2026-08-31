using RpgWorld.Domain.Worlds;

namespace RpgWorld.Domain.Actors;

public sealed class PlayerActor : Actor
{
    private PlayerActor() { }
    private PlayerActor(string name, World world, Position position, int maximumHealth, DateTimeOffset createdAtUtc)
        : base(name, world, position, maximumHealth, createdAtUtc) => RecordCreation(createdAtUtc);
    public override string Kind => "player";
    public static PlayerActor Create(string name, World world, Position position, DateTimeOffset createdAtUtc, int maximumHealth = 100) =>
        new(name, world, position, maximumHealth, createdAtUtc);
}

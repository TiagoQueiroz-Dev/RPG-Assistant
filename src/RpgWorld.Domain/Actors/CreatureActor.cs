using RpgWorld.Domain.Worlds;

namespace RpgWorld.Domain.Actors;

public sealed class CreatureActor : Actor
{
    private CreatureActor() { }
    private CreatureActor(string name, World world, Position position, int maximumHealth, DateTimeOffset createdAtUtc)
        : base(name, world, position, maximumHealth, createdAtUtc) => RecordCreation(createdAtUtc);
    public override string Kind => "creature";
    public static CreatureActor Create(string name, World world, Position position, DateTimeOffset createdAtUtc, int maximumHealth = 100) =>
        new(name, world, position, maximumHealth, createdAtUtc);
}

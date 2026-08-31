using RpgWorld.Domain.Worlds;

namespace RpgWorld.Domain.Actors.Housing;

public enum HousingConstructionStatus { InProgress, Completed }

public sealed class HousingConstruction
{
    private List<Guid> _residentActorIds = [];

    private HousingConstruction() { }

    private HousingConstruction(NpcActor owner, Position position, int requiredWood, int requiredStone, DateTimeOffset createdAt)
    {
        if (owner.WorldId != position.WorldId) throw new ArgumentException("Construction must belong to the owner's world.", nameof(position));
        if (requiredWood <= 0 || requiredStone <= 0) throw new ArgumentOutOfRangeException(nameof(requiredWood));
        Id = Guid.CreateVersion7();
        WorldId = owner.WorldId;
        OwnerActorId = owner.Id;
        _residentActorIds = [owner.Id, .. owner.FamilyIds.Where(actorId => actorId != owner.Id)];
        X = position.X;
        Y = position.Y;
        RequiredWood = requiredWood;
        RequiredStone = requiredStone;
        Status = HousingConstructionStatus.InProgress;
        CreatedAt = createdAt.ToUniversalTime();
        UpdatedAt = CreatedAt;
    }

    public Guid Id { get; private set; }
    public Guid WorldId { get; private set; }
    public Guid OwnerActorId { get; private set; }
    public IReadOnlyList<Guid> ResidentActorIds => _residentActorIds;
    public int X { get; private set; }
    public int Y { get; private set; }
    public Position Position => new(WorldId, X, Y);
    public int RequiredWood { get; private set; }
    public int RequiredStone { get; private set; }
    public int ConsumedWood { get; private set; }
    public int ConsumedStone { get; private set; }
    public int Progress { get; private set; }
    public HousingConstructionStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }

    public static HousingConstruction Create(NpcActor owner, Position position, int requiredWood, int requiredStone, DateTimeOffset createdAt) =>
        new(owner, position, requiredWood, requiredStone, createdAt);

    public bool CanAdvance(NpcActor owner)
    {
        EnsureOwner(owner);
        if (Status == HousingConstructionStatus.Completed) return false;
        var wood = Progress == 0 ? (RequiredWood + 1) / 2 : RequiredWood - ConsumedWood;
        var stone = Progress == 0 ? (RequiredStone + 1) / 2 : RequiredStone - ConsumedStone;
        return owner.InventoryQuantity("wood") >= wood && owner.InventoryQuantity("stone") >= stone;
    }

    public void Advance(NpcActor owner, DateTimeOffset worldInstant)
    {
        EnsureOwner(owner);
        if (Status == HousingConstructionStatus.Completed) throw new InvalidOperationException("Construction is already complete.");
        var isFirstStage = Progress == 0;
        var wood = isFirstStage ? (RequiredWood + 1) / 2 : RequiredWood - ConsumedWood;
        var stone = isFirstStage ? (RequiredStone + 1) / 2 : RequiredStone - ConsumedStone;
        if (!CanAdvance(owner)) throw new InvalidOperationException("NPC lacks resources for the next construction stage.");
        if (wood > 0) owner.ConsumeInventory("wood", wood, worldInstant);
        if (stone > 0) owner.ConsumeInventory("stone", stone, worldInstant);
        ConsumedWood += wood;
        ConsumedStone += stone;
        Progress = isFirstStage ? 50 : 100;
        UpdatedAt = worldInstant.ToUniversalTime();
        if (Progress == 100)
        {
            Status = HousingConstructionStatus.Completed;
            CompletedAt = UpdatedAt;
        }
    }

    private void EnsureOwner(NpcActor owner)
    {
        ArgumentNullException.ThrowIfNull(owner);
        if (owner.Id != OwnerActorId) throw new InvalidOperationException("Only the construction owner can advance this house.");
    }
}

public static class NpcGoalCodes
{
    public const string NeedHouse = "need-house";
}

using RpgWorld.Domain.Worlds;
using RpgWorld.Domain.Actors.Traits;

namespace RpgWorld.Domain.Actors;

public sealed class NpcActor : Actor
{
    public const decimal DefaultHungerPerWorldHour = 4m;
    public const decimal DefaultEnergyPerWorldHour = 3m;

    private List<Guid> _familyIds = [];
    private List<NpcGoal> _goals = [];
    private List<string> _traitCodes = [];

    private NpcActor() { }
    private NpcActor(string name, World world, Position position, int maximumHealth, DateTimeOffset createdAtUtc)
        : base(name, world, position, maximumHealth, createdAtUtc)
    {
        Hunger = 0m;
        Energy = 100m;
        NeedsUpdatedAt = createdAtUtc.ToUniversalTime();
        RecordCreation(createdAtUtc);
    }

    public override string Kind => "npc";

    public decimal Hunger { get; private set; }
    public decimal Energy { get; private set; }
    public decimal Money { get; private set; }
    public string? Job { get; private set; }
    public int? HomeX { get; private set; }
    public int? HomeY { get; private set; }
    public Position? Home => HomeX.HasValue && HomeY.HasValue
        ? new Position(WorldId, HomeX.Value, HomeY.Value)
        : null;
    public DateTimeOffset NeedsUpdatedAt { get; private set; }
    public IReadOnlyList<Guid> FamilyIds => _familyIds;
    public IReadOnlyList<NpcGoal> Goals => _goals.OrderByDescending(goal => goal.Priority).ToArray();
    public IReadOnlyList<string> TraitCodes => _traitCodes.Order(StringComparer.Ordinal).ToArray();

    public static NpcActor Create(string name, World world, Position position, DateTimeOffset createdAtUtc, int maximumHealth = 100) =>
        new(name, world, position, maximumHealth, createdAtUtc);

    public void AdvanceNeedsTo(
        DateTimeOffset worldInstant,
        decimal hungerPerWorldHour = DefaultHungerPerWorldHour,
        decimal energyPerWorldHour = DefaultEnergyPerWorldHour)
    {
        EnsureAlive();
        if (hungerPerWorldHour < 0) throw new ArgumentOutOfRangeException(nameof(hungerPerWorldHour));
        if (energyPerWorldHour < 0) throw new ArgumentOutOfRangeException(nameof(energyPerWorldHour));
        var instant = worldInstant.ToUniversalTime();
        if (instant < NeedsUpdatedAt) throw new ArgumentOutOfRangeException(nameof(worldInstant), "World time cannot move backwards.");
        var elapsedHours = (decimal)(instant - NeedsUpdatedAt).TotalHours;
        Hunger = ClampNeed(Hunger + elapsedHours * hungerPerWorldHour);
        Energy = ClampNeed(Energy - elapsedHours * energyPerWorldHour);
        NeedsUpdatedAt = instant;
        Touch(instant);
    }

    public void Eat(decimal nutrition, DateTimeOffset worldInstant)
    {
        EnsureAlive();
        if (nutrition <= 0) throw new ArgumentOutOfRangeException(nameof(nutrition));
        AdvanceNeedsTo(worldInstant);
        Hunger = ClampNeed(Hunger - nutrition);
        Touch(worldInstant);
    }

    public void Rest(decimal recoveredEnergy, DateTimeOffset worldInstant)
    {
        EnsureAlive();
        if (recoveredEnergy <= 0) throw new ArgumentOutOfRangeException(nameof(recoveredEnergy));
        AdvanceNeedsTo(worldInstant);
        Energy = ClampNeed(Energy + recoveredEnergy);
        Touch(worldInstant);
    }

    public void ConsumeEnergy(decimal amount, DateTimeOffset worldInstant)
    {
        EnsureAlive();
        if (amount <= 0) throw new ArgumentOutOfRangeException(nameof(amount));
        AdvanceNeedsTo(worldInstant);
        Energy = ClampNeed(Energy - amount);
        Touch(worldInstant);
    }

    public void Earn(decimal amount, DateTimeOffset worldInstant)
    {
        EnsureAlive();
        if (amount <= 0) throw new ArgumentOutOfRangeException(nameof(amount));
        AdvanceNeedsTo(worldInstant);
        Money = checked(Money + amount);
        Touch(worldInstant);
    }

    public void Spend(decimal amount, DateTimeOffset worldInstant)
    {
        EnsureAlive();
        if (amount <= 0 || amount > Money) throw new ArgumentOutOfRangeException(nameof(amount), "NPC cannot spend more money than available.");
        AdvanceNeedsTo(worldInstant);
        Money -= amount;
        Touch(worldInstant);
    }

    public void AssignJob(string? job, DateTimeOffset worldInstant)
    {
        EnsureAlive();
        if (job is { Length: > 120 }) throw new ArgumentException("Job is too long.", nameof(job));
        AdvanceNeedsTo(worldInstant);
        Job = string.IsNullOrWhiteSpace(job) ? null : job.Trim();
        Touch(worldInstant);
    }

    public void SetHome(World world, Position? home, DateTimeOffset worldInstant)
    {
        EnsureAlive();
        ArgumentNullException.ThrowIfNull(world);
        if (world.Id != WorldId || (home.HasValue && !world.Contains(home.Value)))
            throw new ArgumentOutOfRangeException(nameof(home), "Home must be inside the NPC's world.");
        AdvanceNeedsTo(worldInstant);
        HomeX = home?.X;
        HomeY = home?.Y;
        Touch(worldInstant);
    }

    public void AddFamilyMember(Guid actorId, DateTimeOffset worldInstant)
    {
        EnsureAlive();
        if (actorId == Guid.Empty || actorId == Id) throw new ArgumentException("Family member is invalid.", nameof(actorId));
        AdvanceNeedsTo(worldInstant);
        if (!_familyIds.Contains(actorId)) _familyIds.Add(actorId);
        Touch(worldInstant);
    }

    public void SetGoal(string code, int priority, Guid? targetId, DateTimeOffset worldInstant)
    {
        EnsureAlive();
        var goal = new NpcGoal(code, priority, targetId);
        AdvanceNeedsTo(worldInstant);
        _goals.RemoveAll(existing => string.Equals(existing.Code, goal.Code, StringComparison.OrdinalIgnoreCase));
        _goals.Add(goal);
        Touch(worldInstant);
    }

    public void AddTrait(TraitDefinition trait, DateTimeOffset worldInstant)
    {
        EnsureAlive();
        ArgumentNullException.ThrowIfNull(trait);
        AdvanceNeedsTo(worldInstant);
        if (!_traitCodes.Contains(trait.Code, StringComparer.OrdinalIgnoreCase))
            _traitCodes.Add(trait.Code);
        Touch(worldInstant);
    }

    public void RemoveTrait(string traitCode, DateTimeOffset worldInstant)
    {
        EnsureAlive();
        if (string.IsNullOrWhiteSpace(traitCode)) throw new ArgumentException("Trait code is required.", nameof(traitCode));
        AdvanceNeedsTo(worldInstant);
        _traitCodes.RemoveAll(code => string.Equals(code, traitCode.Trim(), StringComparison.OrdinalIgnoreCase));
        Touch(worldInstant);
    }

    private static decimal ClampNeed(decimal value) => Math.Clamp(value, 0m, 100m);
}

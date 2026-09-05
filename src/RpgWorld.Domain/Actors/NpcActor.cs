using RpgWorld.Domain.Worlds;
using RpgWorld.Domain.Actors.Traits;
using RpgWorld.Domain.Worlds.Cities;
using RpgWorld.Domain.Actors.Actions;
using RpgWorld.Domain.Events;

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
    public Guid? HomeStructureId { get; private set; }
    public Guid? ResidentCityId { get; private set; }
    public Position? Home => HomeX.HasValue && HomeY.HasValue
        ? new Position(WorldId, HomeX.Value, HomeY.Value)
        : null;
    public DateTimeOffset NeedsUpdatedAt { get; private set; }
    public IReadOnlyList<Guid> FamilyIds => _familyIds;
    public IReadOnlyList<NpcGoal> Goals => _goals.OrderByDescending(goal => goal.Priority).ToArray();
    public IReadOnlyList<string> TraitCodes => _traitCodes.Order(StringComparer.Ordinal).ToArray();
    public NpcActionExecution? ActionExecution { get; private set; }

    public override void SetCurrentAction(string? action, DateTimeOffset occurredAtUtc) =>
        SelectAction(action, occurredAtUtc);

    public bool SelectAction(string? code, DateTimeOffset instant, NpcActionTarget? target = null,
        NpcActionReplacementPolicy policy = NpcActionReplacementPolicy.ReplaceDifferent)
    {
        EnsureAlive();
        if (!Enum.IsDefined(policy)) throw new ArgumentOutOfRangeException(nameof(policy));
        if (target?.Position is { } position && position.WorldId != WorldId)
            throw new ArgumentException("Action target must belong to this world.", nameof(target));
        var normalized = string.IsNullOrWhiteSpace(code) ? null : code.Trim();
        if (ActionExecution is { Status: NpcActionStatus.Running } running)
        {
            if (instant < running.UpdatedAt) throw new ArgumentOutOfRangeException(nameof(instant));
            if (policy == NpcActionReplacementPolicy.KeepRunning ||
                (policy == NpcActionReplacementPolicy.ReplaceDifferent && running.ActionCode == normalized &&
                 (target is null || target == running.Target))) return false;
        }
        // Validate the replacement before cancelling the previous action.
        var next = normalized is null ? null : NpcActionExecution.Start(normalized, instant, target);
        var changed = false;
        if (ActionExecution is { Status: NpcActionStatus.Running } previous)
        {
            ApplyExecution(previous.Finish(NpcActionStatus.Cancelled, instant,
                normalized is null ? "No action selected." : "Replaced by a new decision."));
            changed = true;
        }
        if (next is not null) { ApplyExecution(next); changed = true; }
        base.SetCurrentAction(normalized, instant);
        return changed;
    }

    public void SetActionTarget(Guid executionId, NpcActionTarget target, DateTimeOffset instant)
    {
        ArgumentNullException.ThrowIfNull(target);
        if (target.Position is { } position && position.WorldId != WorldId)
            throw new ArgumentException("Action target must belong to this world.", nameof(target));
        ApplyExecution(RequiredExecution(executionId).Retarget(target, instant));
    }

    public void AdvanceAction(Guid executionId, decimal progress, DateTimeOffset instant) =>
        ApplyExecution(RequiredExecution(executionId).Advance(progress, instant));

    public void FinishAction(Guid executionId, NpcActionStatus status, DateTimeOffset instant, string? reason = null)
    {
        ApplyExecution(RequiredExecution(executionId).Finish(status, instant, reason));
        base.SetCurrentAction(null, instant);
    }

    private NpcActionExecution RequiredExecution(Guid id)
    {
        EnsureAlive();
        return ActionExecution is { } execution && execution.Id == id ? execution
            : throw new InvalidOperationException("Action execution has been replaced or is missing.");
    }

    private void ApplyExecution(NpcActionExecution execution)
    {
        if (execution == ActionExecution) return;
        var isStarting = ActionExecution?.Id != execution.Id;
        ActionExecution = execution;
        Touch(execution.UpdatedAt);
        RaiseDomainEvent(new NpcActionExecutionChangedEvent(Id, WorldId, Position, execution, isStarting));
    }

    protected override void OnActionInterrupted(DateTimeOffset occurredAtUtc)
    {
        if (ActionExecution is { Status: NpcActionStatus.Running } running)
            ApplyExecution(running.Finish(NpcActionStatus.Cancelled, occurredAtUtc,
                Status == ActorStatus.Dead ? "Actor died." : "Interrupted by damage."));
    }

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

    public void SetHome(World world, Position? home, DateTimeOffset worldInstant, Guid? structureId = null)
    {
        EnsureAlive();
        ArgumentNullException.ThrowIfNull(world);
        if (world.Id != WorldId || (home.HasValue && !world.Contains(home.Value)))
            throw new ArgumentOutOfRangeException(nameof(home), "Home must be inside the NPC's world.");
        if (structureId == Guid.Empty || (!home.HasValue && structureId.HasValue))
            throw new ArgumentException("A home structure requires a valid home position.", nameof(structureId));
        AdvanceNeedsTo(worldInstant);
        HomeX = home?.X;
        HomeY = home?.Y;
        HomeStructureId = structureId;
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

    public void JoinCity(City city, DateTimeOffset worldInstant)
    {
        EnsureAlive();
        ArgumentNullException.ThrowIfNull(city);
        if (city.WorldId != WorldId || city.Status == CityStatus.Destroyed)
            throw new InvalidOperationException("NPC can only join an active city in the same world.");
        if (ResidentCityId is { } current && current != city.Id)
            throw new InvalidOperationException("NPC already resides in another city.");
        AdvanceNeedsTo(worldInstant);
        ResidentCityId = city.Id;
        Touch(worldInstant);
    }

    public void LeaveCity(Guid cityId, DateTimeOffset worldInstant)
    {
        if (cityId == Guid.Empty) throw new ArgumentException("City identifier cannot be empty.", nameof(cityId));
        if (ResidentCityId != cityId) return;
        if (Status != ActorStatus.Dead) AdvanceNeedsTo(worldInstant);
        ResidentCityId = null;
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

    public void RemoveGoal(string code, DateTimeOffset worldInstant)
    {
        EnsureAlive();
        if (string.IsNullOrWhiteSpace(code)) throw new ArgumentException("Goal code is required.", nameof(code));
        AdvanceNeedsTo(worldInstant);
        _goals.RemoveAll(goal => string.Equals(goal.Code, code.Trim(), StringComparison.OrdinalIgnoreCase));
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

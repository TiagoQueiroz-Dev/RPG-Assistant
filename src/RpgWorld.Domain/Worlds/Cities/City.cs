using RpgWorld.Domain.Events;
using RpgWorld.Domain.Worlds.Definitions;

namespace RpgWorld.Domain.Worlds.Cities;

public enum CityStatus { Active, Crisis, Destroyed }

public sealed class City : AggregateRoot
{
    public const int MaximumTerritoryTiles = 4096;

    private List<CityTerritoryTile> _territoryTiles = [];
    private List<Guid> _residentActorIds = [];
    private List<Guid> _buildingIds = [];
    private Dictionary<string, decimal> _resourceStocks = [];
    private Dictionary<string, CityResourceMarketSnapshot> _resourceMarkets = [];
    private List<CityHistoryEntry> _history = [];

    private City() { }

    private City(
        World world,
        string name,
        Position center,
        IReadOnlyCollection<Position> territory,
        int initialPopulation,
        decimal initialWealth,
        Guid? governingFactionId,
        DateTimeOffset foundedAtUtc)
    {
        Id = Guid.CreateVersion7();
        WorldId = world.Id;
        Name = RequiredText(name, nameof(name), 200);
        CenterX = center.X;
        CenterY = center.Y;
        Population = initialPopulation;
        Wealth = initialWealth;
        GoverningFactionId = governingFactionId;
        Status = CityStatus.Active;
        FoundedAtUtc = foundedAtUtc.ToUniversalTime();
        UpdatedAtUtc = FoundedAtUtc;
        _territoryTiles = territory.Select(position => CityTerritoryTile.Create(Id, position)).ToList();
        AddHistory(CityHistoryEventTypes.Founded, $"City {Name} was founded.", FoundedAtUtc);
        RaiseDomainEvent(new CityCreatedEvent(Id, WorldId, Name, FoundedAtUtc));
    }

    public Guid Id { get; private set; }
    public Guid WorldId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public int CenterX { get; private set; }
    public int CenterY { get; private set; }
    public Position Center => new(WorldId, CenterX, CenterY);
    public int Population { get; private set; }
    public decimal Wealth { get; private set; }
    public Guid? GoverningFactionId { get; private set; }
    public CityStatus Status { get; private set; }
    public DateTimeOffset FoundedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public DateTimeOffset? DestroyedAtUtc { get; private set; }
    public long Version { get; private set; }
    public long EconomicCycleCount { get; private set; }
    public DateTimeOffset? LastEconomicCycleAtUtc { get; private set; }
    public IReadOnlyList<CityTerritoryTile> TerritoryTiles => _territoryTiles.ToArray();
    public IReadOnlyList<Position> Territory => _territoryTiles.Select(tile => tile.Position).ToArray();
    public IReadOnlyList<Guid> ResidentActorIds => _residentActorIds.ToArray();
    public IReadOnlyList<Guid> BuildingIds => _buildingIds.ToArray();
    public IReadOnlyDictionary<string, decimal> ResourceStocks => new Dictionary<string, decimal>(_resourceStocks);
    public IReadOnlyDictionary<string, CityResourceMarketSnapshot> ResourceMarkets =>
        new Dictionary<string, CityResourceMarketSnapshot>(_resourceMarkets);
    public IReadOnlyList<CityHistoryEntry> History => _history
        .Select(entry => entry with { Metadata = new Dictionary<string, string>(entry.Metadata) })
        .ToArray();

    public static City Create(
        World world,
        string name,
        Position center,
        IEnumerable<Position> territory,
        int initialPopulation,
        decimal initialWealth,
        DateTimeOffset foundedAtUtc,
        Guid? governingFactionId = null)
    {
        ArgumentNullException.ThrowIfNull(world);
        if (!world.Contains(center)) throw new ArgumentOutOfRangeException(nameof(center));
        if (initialPopulation < 0) throw new ArgumentOutOfRangeException(nameof(initialPopulation));
        if (initialWealth < 0m) throw new ArgumentOutOfRangeException(nameof(initialWealth));
        if (governingFactionId == Guid.Empty) throw new ArgumentException("Faction identifier cannot be empty.", nameof(governingFactionId));
        var validated = ValidateTerritory(world, center, territory);
        return new City(world, name, center, validated, initialPopulation, initialWealth, governingFactionId, foundedAtUtc);
    }

    public void ChangePopulation(int delta, string reason, DateTimeOffset occurredAtUtc)
    {
        EnsureActive();
        if (delta == 0) throw new ArgumentOutOfRangeException(nameof(delta));
        var next = checked(Population + delta);
        if (next < _residentActorIds.Count)
            throw new InvalidOperationException("Population cannot be lower than the number of named residents.");
        var description = RequiredText(reason, nameof(reason), 500);
        var previous = Population;
        Population = next;
        Touch(occurredAtUtc);
        if (delta > 0)
        {
            AddHistory(CityHistoryEventTypes.Growth, description, occurredAtUtc);
            RaiseDomainEvent(new CityGrowthEvent(Id, WorldId, previous, Population, description, UpdatedAtUtc));
        }
        else
        {
            AddHistory(CityHistoryEventTypes.Decline, description, occurredAtUtc);
            RaiseDomainEvent(new CityPopulationChangedEvent(Id, WorldId, previous, Population, description, UpdatedAtUtc));
        }
    }

    public bool AddResident(Guid actorId, DateTimeOffset occurredAtUtc, bool increasePopulation = true)
    {
        EnsureActive();
        RequiredId(actorId, nameof(actorId));
        if (_residentActorIds.Contains(actorId)) return false;
        if (increasePopulation && Population == int.MaxValue) throw new OverflowException("City population is at its maximum value.");
        if (!increasePopulation && Population < _residentActorIds.Count + 1)
            throw new InvalidOperationException("Initial population cannot be lower than named residents.");
        _residentActorIds.Add(actorId);
        if (increasePopulation)
            ChangePopulation(1, "A resident joined the city.", occurredAtUtc);
        else
        {
            Touch(occurredAtUtc);
            AddHistory(
                CityHistoryEventTypes.ResidentAssociated,
                $"Resident {actorId} was associated with the city.",
                occurredAtUtc);
        }
        return true;
    }

    public bool RemoveResident(Guid actorId, string reason, DateTimeOffset occurredAtUtc, bool decreasePopulation = true)
    {
        EnsureActive();
        if (decreasePopulation) RequiredText(reason, nameof(reason), 500);
        if (!_residentActorIds.Remove(actorId)) return false;
        if (decreasePopulation)
            ChangePopulation(-1, reason, occurredAtUtc);
        else Touch(occurredAtUtc);
        return true;
    }

    public void StoreResource(string resourceCode, decimal quantity, DateTimeOffset occurredAtUtc)
    {
        EnsureActive();
        if (quantity <= 0m) throw new ArgumentOutOfRangeException(nameof(quantity));
        var code = DefinitionCode.Normalize(resourceCode, nameof(resourceCode));
        _resourceStocks[code] = checked(_resourceStocks.GetValueOrDefault(code) + quantity);
        Touch(occurredAtUtc);
    }

    public void ConsumeResource(string resourceCode, decimal quantity, DateTimeOffset occurredAtUtc)
    {
        EnsureActive();
        if (quantity <= 0m) throw new ArgumentOutOfRangeException(nameof(quantity));
        var code = DefinitionCode.Normalize(resourceCode, nameof(resourceCode));
        if (_resourceStocks.GetValueOrDefault(code) < quantity)
            throw new InvalidOperationException($"City lacks {quantity} units of '{code}'.");
        var remaining = _resourceStocks[code] - quantity;
        if (remaining == 0m) _resourceStocks.Remove(code);
        else _resourceStocks[code] = remaining;
        Touch(occurredAtUtc);
    }

    public CityEconomicCycleResult RunEconomicCycle(
        IEnumerable<CityResourceEconomyRule> rules,
        IReadOnlyDictionary<string, decimal> production,
        DateTimeOffset occurredAtUtc)
    {
        EnsureActive();
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(production);
        var instant = occurredAtUtc.ToUniversalTime();
        if (LastEconomicCycleAtUtc is { } last && instant <= last)
            throw new ArgumentOutOfRangeException(nameof(occurredAtUtc), "Economic cycles must move forward in world time.");
        var configuredRules = rules.ToArray();
        if (configuredRules.Length == 0) throw new ArgumentException("At least one economy rule is required.", nameof(rules));
        if (configuredRules.Select(rule => rule.ResourceCode).Distinct(StringComparer.Ordinal).Count() != configuredRules.Length)
            throw new ArgumentException("Economy resource rules must be unique.", nameof(rules));
        if (production.Any(entry => entry.Value < 0m))
            throw new ArgumentOutOfRangeException(nameof(production), "Production cannot be negative.");

        var markets = new List<CityResourceMarketSnapshot>(configuredRules.Length);
        foreach (var rule in configuredRules.OrderBy(rule => rule.ResourceCode, StringComparer.Ordinal))
        {
            var code = rule.ResourceCode;
            var openingStock = _resourceStocks.GetValueOrDefault(code);
            var produced = production.GetValueOrDefault(code);
            var available = checked(openingStock + produced);
            var demand = checked(Population * rule.ConsumptionPerResident);
            var consumed = Math.Min(available, demand);
            var unmetDemand = demand - consumed;
            var closingStock = available - consumed;
            if (closingStock == 0m) _resourceStocks.Remove(code);
            else _resourceStocks[code] = closingStock;

            var targetStock = Math.Max(rule.TargetStockPerResident, Population * rule.TargetStockPerResident);
            var coverage = closingStock / targetStock;
            var priceMultiplier = coverage < 1m
                ? 1m + ((1m - coverage) * (rule.MaximumPriceMultiplier - 1m))
                : Math.Max(rule.MinimumPriceMultiplier, 1m / coverage);
            var price = decimal.Round(rule.BasePrice * priceMultiplier, 2, MidpointRounding.AwayFromZero);
            var condition = unmetDemand > 0m || coverage <= rule.CriticalStockRatio
                ? CityMarketCondition.Shortage
                : coverage >= rule.SurplusStockRatio
                    ? CityMarketCondition.Surplus
                    : CityMarketCondition.Balanced;
            var previousCondition = _resourceMarkets.TryGetValue(code, out var previous)
                ? previous.Condition
                : CityMarketCondition.Balanced;
            var market = new CityResourceMarketSnapshot(
                code, openingStock, produced, demand, consumed, unmetDemand, closingStock, price, condition, instant);
            _resourceMarkets[code] = market;
            markets.Add(market);
            RecordMarketTransition(previousCondition, market);
        }

        EconomicCycleCount = checked(EconomicCycleCount + 1);
        LastEconomicCycleAtUtc = instant;
        Touch(instant);
        return new CityEconomicCycleResult(Id, EconomicCycleCount, instant, markets.ToArray());
    }

    public void AddBuilding(Guid buildingId, DateTimeOffset occurredAtUtc)
    {
        EnsureActive();
        RequiredId(buildingId, nameof(buildingId));
        if (!_buildingIds.Contains(buildingId)) _buildingIds.Add(buildingId);
        Touch(occurredAtUtc);
    }

    public void CreditWealth(decimal amount, DateTimeOffset occurredAtUtc)
    {
        EnsureActive();
        if (amount <= 0m) throw new ArgumentOutOfRangeException(nameof(amount));
        Wealth = checked(Wealth + amount);
        Touch(occurredAtUtc);
    }

    public void DebitWealth(decimal amount, DateTimeOffset occurredAtUtc)
    {
        EnsureActive();
        if (amount <= 0m || amount > Wealth) throw new ArgumentOutOfRangeException(nameof(amount));
        Wealth -= amount;
        Touch(occurredAtUtc);
    }

    public void SetGoverningFaction(Guid? factionId, DateTimeOffset occurredAtUtc)
    {
        EnsureActive();
        if (factionId == Guid.Empty) throw new ArgumentException("Faction identifier cannot be empty.", nameof(factionId));
        GoverningFactionId = factionId;
        Touch(occurredAtUtc);
    }

    public void BeginCrisis(string reason, int severity, DateTimeOffset occurredAtUtc)
    {
        EnsureActive();
        if (Status == CityStatus.Crisis) throw new InvalidOperationException("City is already in crisis.");
        if (severity is < 1 or > 100) throw new ArgumentOutOfRangeException(nameof(severity));
        var description = RequiredText(reason, nameof(reason), 500);
        Status = CityStatus.Crisis;
        Touch(occurredAtUtc);
        AddHistory(CityHistoryEventTypes.Crisis, description, occurredAtUtc,
            new Dictionary<string, string> { ["severity"] = severity.ToString() });
        RaiseDomainEvent(new CityCrisisEvent(Id, WorldId, description, severity, UpdatedAtUtc));
    }

    public void ResolveCrisis(string resolution, DateTimeOffset occurredAtUtc)
    {
        if (Status != CityStatus.Crisis) throw new InvalidOperationException("City is not in crisis.");
        Status = CityStatus.Active;
        Touch(occurredAtUtc);
        AddHistory(CityHistoryEventTypes.CrisisResolved, RequiredText(resolution, nameof(resolution), 500), occurredAtUtc);
    }

    public void Destroy(string reason, DateTimeOffset destroyedAtUtc)
    {
        EnsureActive();
        var description = RequiredText(reason, nameof(reason), 500);
        var finalPopulation = Population;
        var formerResidents = string.Join(',', _residentActorIds);
        Status = CityStatus.Destroyed;
        Population = 0;
        _residentActorIds.Clear();
        DestroyedAtUtc = destroyedAtUtc.ToUniversalTime();
        foreach (var territoryTile in _territoryTiles) territoryTile.Release(DestroyedAtUtc.Value);
        Touch(DestroyedAtUtc.Value);
        AddHistory(CityHistoryEventTypes.Destroyed, description, DestroyedAtUtc.Value,
            new Dictionary<string, string>
            {
                ["finalPopulation"] = finalPopulation.ToString(),
                ["formerResidentActorIds"] = formerResidents
            });
        RaiseDomainEvent(new CityDestroyedEvent(Id, WorldId, description, finalPopulation, DestroyedAtUtc.Value));
    }

    private void EnsureActive()
    {
        if (Status == CityStatus.Destroyed) throw new InvalidOperationException("Destroyed city cannot change.");
    }

    private void RecordMarketTransition(
        CityMarketCondition previousCondition,
        CityResourceMarketSnapshot market)
    {
        if (market.Condition == previousCondition) return;
        var metadata = new Dictionary<string, string>
        {
            ["resourceCode"] = market.ResourceCode,
            ["closingStock"] = market.ClosingStock.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["unitPrice"] = market.UnitPrice.ToString(System.Globalization.CultureInfo.InvariantCulture)
        };
        switch (market.Condition)
        {
            case CityMarketCondition.Shortage:
                AddHistory(
                    CityHistoryEventTypes.ResourceShortage,
                    $"Critical shortage of {market.ResourceCode}.",
                    market.UpdatedAtUtc,
                    metadata);
                RaiseDomainEvent(new CityResourceShortageEvent(
                    Id, WorldId, market.ResourceCode, market.Demand, market.Consumed,
                    market.ClosingStock, market.UnitPrice, market.UpdatedAtUtc));
                break;
            case CityMarketCondition.Surplus:
                AddHistory(
                    CityHistoryEventTypes.ResourceSurplus,
                    $"Surplus of {market.ResourceCode}.",
                    market.UpdatedAtUtc,
                    metadata);
                RaiseDomainEvent(new CityResourceSurplusEvent(
                    Id, WorldId, market.ResourceCode, market.Produced,
                    market.ClosingStock, market.UnitPrice, market.UpdatedAtUtc));
                break;
            case CityMarketCondition.Balanced:
                AddHistory(
                    CityHistoryEventTypes.EconomyBalanced,
                    $"Supply of {market.ResourceCode} returned to balance.",
                    market.UpdatedAtUtc,
                    metadata);
                break;
        }
    }

    private void Touch(DateTimeOffset occurredAtUtc)
    {
        var instant = occurredAtUtc.ToUniversalTime();
        if (instant < UpdatedAtUtc) throw new ArgumentOutOfRangeException(nameof(occurredAtUtc), "City time cannot move backwards.");
        UpdatedAtUtc = instant;
        Version = checked(Version + 1);
    }

    private void AddHistory(
        string eventType,
        string description,
        DateTimeOffset occurredAtUtc,
        Dictionary<string, string>? metadata = null) =>
        _history.Add(new CityHistoryEntry(
            Guid.CreateVersion7(), eventType, description, Population, occurredAtUtc, metadata));

    private static IReadOnlyCollection<Position> ValidateTerritory(
        World world,
        Position center,
        IEnumerable<Position> territory)
    {
        ArgumentNullException.ThrowIfNull(territory);
        var positions = territory.Distinct().ToArray();
        if (positions.Length is 0 or > MaximumTerritoryTiles)
            throw new ArgumentOutOfRangeException(nameof(territory), $"Territory must contain between 1 and {MaximumTerritoryTiles} tiles.");
        if (positions.Any(position => !world.Contains(position)))
            throw new ArgumentOutOfRangeException(nameof(territory), "Every territory tile must be inside the city world.");
        if (!positions.Contains(center)) throw new ArgumentException("Territory must contain the city center.", nameof(territory));
        var all = positions.ToHashSet();
        var visited = new HashSet<Position> { center };
        var queue = new Queue<Position>([center]);
        while (queue.TryDequeue(out var current))
        {
            foreach (var (dx, dy) in new[] { (-1, 0), (1, 0), (0, -1), (0, 1) })
            {
                var x = current.X + dx;
                var y = current.Y + dy;
                if (x < 0 || y < 0 || x >= world.Width || y >= world.Height) continue;
                var adjacent = new Position(world.Id, x, y);
                if (all.Contains(adjacent) && visited.Add(adjacent)) queue.Enqueue(adjacent);
            }
        }
        if (visited.Count != positions.Length)
            throw new ArgumentException("City territory must form one contiguous area.", nameof(territory));
        return positions;
    }

    private static Guid RequiredId(Guid id, string parameterName) =>
        id == Guid.Empty ? throw new ArgumentException("Identifier cannot be empty.", parameterName) : id;

    private static string RequiredText(string value, string parameterName, int maximumLength)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("Text cannot be empty.", parameterName);
        var normalized = value.Trim();
        if (normalized.Length > maximumLength) throw new ArgumentException($"Text cannot exceed {maximumLength} characters.", parameterName);
        return normalized;
    }
}

using RpgWorld.Application.Worlds.Cities;
using RpgWorld.Domain.Worlds.Cities;
using RpgWorld.Domain.Worlds.Resources;
using RpgWorld.Simulation.Engine;

namespace RpgWorld.Simulation.Worlds.Economy;

public sealed class CityEconomySimulationSystem(
    ICityEconomyRepository repository,
    CityEconomyOptions options) : ISimulationSystem
{
    public string Name => "CityEconomy";
    public int Order => 40;
    public TimeSpan Frequency => SimulationSystemFrequencies.Economy;

    public async Task ExecuteAsync(SimulationTickContext context, CancellationToken cancellationToken = default)
    {
        var cities = await repository.ListSimulatedCitiesAsync(context.WorldId, cancellationToken);
        if (cities.Count == 0) return;
        var naturalCodes = options.Resources.Select(resource => resource.NormalizedNaturalResourceCode)
            .OfType<string>().Distinct(StringComparer.Ordinal).ToArray();
        foreach (var city in cities)
        {
            var merchants = await repository.ListActiveMerchantsAsync(city, cancellationToken);
            context.RecordActorsProcessed(merchants.Count);
            var deposits = await repository.ListAvailableDepositsAsync(city, naturalCodes, cancellationToken);
            var production = Produce(city, deposits, merchants.Count, context.Clock.CurrentInstant);
            city.RunEconomicCycle(options.Resources.Select(resource => resource.ToRule()).ToArray(), production,
                context.Clock.CurrentInstant, merchants.Count);
        }
        await repository.SaveChangesAsync(cancellationToken);
    }

    private IReadOnlyDictionary<string, decimal> Produce(
        City city,
        IReadOnlyList<ResourceDeposit> deposits,
        int activeMerchantCount,
        DateTimeOffset occurredAtUtc)
    {
        var production = new Dictionary<string, decimal>(StringComparer.Ordinal);
        foreach (var resource in options.Resources)
        {
            var produced = checked(
                (city.Population * resource.BaselineProductionPerResident) +
                (city.BuildingIds.Count * resource.ProductionPerBuilding) +
                (activeMerchantCount * resource.TradeImportPerMerchant));
            var requestedExtraction = checked(city.Population * resource.NaturalExtractionPerResident);
            if (requestedExtraction > 0m && resource.NormalizedNaturalResourceCode is { } naturalCode)
            {
                foreach (var deposit in deposits.Where(deposit => deposit.ResourceCode == naturalCode))
                {
                    if (requestedExtraction <= 0m) break;
                    var extraction = deposit.Extract(
                        requestedExtraction,
                        ResourceConsumer.City(city.Id),
                        occurredAtUtc);
                    produced = checked(produced + extraction.Quantity);
                    requestedExtraction -= extraction.Quantity;
                }
            }
            production[resource.NormalizedResourceCode] = produced;
        }
        return production;
    }
}

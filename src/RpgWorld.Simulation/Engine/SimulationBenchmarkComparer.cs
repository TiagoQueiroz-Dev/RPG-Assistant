namespace RpgWorld.Simulation.Engine;

public sealed record SimulationBenchmarkMeasurement(int ActorCount, double MeanMilliseconds);

public sealed record SimulationBenchmarkRegression(
    int ActorCount,
    double BaselineMeanMilliseconds,
    double CurrentMeanMilliseconds,
    double RegressionPercent);

public static class SimulationBenchmarkComparer
{
    public static IReadOnlyList<SimulationBenchmarkRegression> Compare(
        IEnumerable<SimulationBenchmarkMeasurement> baseline,
        IEnumerable<SimulationBenchmarkMeasurement> current,
        double maximumRegressionPercent)
    {
        ArgumentNullException.ThrowIfNull(baseline);
        ArgumentNullException.ThrowIfNull(current);
        if (maximumRegressionPercent < 0) throw new ArgumentOutOfRangeException(nameof(maximumRegressionPercent));
        var previousByScale = baseline.Where(value => value.ActorCount > 0 && value.MeanMilliseconds > 0)
            .ToDictionary(value => value.ActorCount);
        return current.Where(value => previousByScale.ContainsKey(value.ActorCount))
            .Select(value =>
            {
                var previous = previousByScale[value.ActorCount];
                var percent = ((value.MeanMilliseconds / previous.MeanMilliseconds) - 1d) * 100d;
                return new SimulationBenchmarkRegression(value.ActorCount, previous.MeanMilliseconds,
                    value.MeanMilliseconds, percent);
            })
            .Where(value => value.RegressionPercent > maximumRegressionPercent)
            .OrderBy(value => value.ActorCount)
            .ToArray();
    }
}

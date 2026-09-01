using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using RpgWorld.Application.Actors;
using RpgWorld.Domain.Actors;
using RpgWorld.Domain.Worlds;
using RpgWorld.Simulation.Actors;
using RpgWorld.Simulation.Engine;
using RpgWorld.Simulation.Time;

var options = BenchmarkOptions.Parse(args);
var results = new List<BenchmarkResult>();
foreach (var actorCount in options.ActorCounts)
    results.Add(await RunAsync(actorCount, options.WarmupIterations, options.Iterations));

var report = new BenchmarkReport(
    DateTimeOffset.UtcNow,
    Environment.Version.ToString(),
    Environment.MachineName,
    options.WarmupIterations,
    options.Iterations,
    results);
var json = JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true });
Console.WriteLine(json);
if (options.OutputPath is not null)
{
    var outputPath = Path.GetFullPath(options.OutputPath);
    Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
    await File.WriteAllTextAsync(outputPath, json);
}

if (options.BaselinePath is not null)
{
    var baseline = JsonSerializer.Deserialize<BenchmarkReport>(await File.ReadAllTextAsync(options.BaselinePath))
        ?? throw new InvalidOperationException("The baseline report is empty.");
    var regressions = SimulationBenchmarkComparer.Compare(
        baseline.Results.Select(value => new SimulationBenchmarkMeasurement(value.ActorCount, value.MeanMilliseconds)),
        report.Results.Select(value => new SimulationBenchmarkMeasurement(value.ActorCount, value.MeanMilliseconds)),
        options.MaximumRegressionPercent);
    foreach (var regression in regressions)
        Console.Error.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"REGRESSION: {regression.ActorCount} NPCs became {regression.RegressionPercent:F1}% slower ({regression.BaselineMeanMilliseconds:F3} ms -> {regression.CurrentMeanMilliseconds:F3} ms; limit {options.MaximumRegressionPercent:F1}%)."));
    if (regressions.Count > 0) Environment.ExitCode = 2;
}

static async Task<BenchmarkResult> RunAsync(int actorCount, int warmups, int iterations)
{
    var createdAt = DateTimeOffset.UnixEpoch;
    var world = World.Create($"NPC benchmark {actorCount}", 128, 128);
    var npcs = Enumerable.Range(0, actorCount)
        .Select(index => NpcActor.Create($"NPC {index}", world,
            world.PositionAt(index % world.Width, (index / world.Width) % world.Height), createdAt))
        .ToArray();
    var repository = new BenchmarkNpcRepository(npcs);
    var system = new NpcNeedsSimulationSystem(repository);
    var instant = createdAt;

    for (var iteration = 0; iteration < warmups; iteration++)
    {
        instant = instant.AddMinutes(1);
        await ExecuteAsync(system, world.Id, instant);
    }

    var samples = new double[iterations];
    for (var iteration = 0; iteration < iterations; iteration++)
    {
        instant = instant.AddMinutes(1);
        var started = Stopwatch.GetTimestamp();
        await ExecuteAsync(system, world.Id, instant);
        samples[iteration] = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
    }

    Array.Sort(samples);
    var mean = samples.Average();
    var percentile95 = samples[Math.Min(samples.Length - 1, (int)Math.Ceiling(samples.Length * 0.95) - 1)];
    return new BenchmarkResult(actorCount, mean, percentile95, samples[^1],
        mean <= 0 ? 0 : actorCount / (mean / 1000d));
}

static Task ExecuteAsync(NpcNeedsSimulationSystem system, Guid worldId, DateTimeOffset instant)
{
    var workload = new SimulationTickWorkload(0);
    return system.ExecuteAsync(new SimulationTickContext(worldId,
        new WorldClockSnapshot(worldId, instant, TimeSpan.FromMinutes(1), 1m, instant), workload));
}

sealed class BenchmarkNpcRepository(IReadOnlyList<NpcActor> npcs) : INpcNeedsRepository
{
    public Task<IReadOnlyList<NpcActor>> ListForUpdateAsync(
        Guid worldId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<NpcActor>>(npcs.Where(npc => npc.WorldId == worldId).ToArray());

    public Task<IReadOnlyList<NpcNeedsSnapshot>> ListUrgentAsync(
        Guid worldId, decimal minimumHunger, decimal maximumEnergy, int limit = 100,
        CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
}

sealed record BenchmarkResult(
    int ActorCount,
    double MeanMilliseconds,
    double Percentile95Milliseconds,
    double MaximumMilliseconds,
    double ActorsPerSecond);

sealed record BenchmarkReport(
    DateTimeOffset CreatedAtUtc,
    string Runtime,
    string Machine,
    int WarmupIterations,
    int Iterations,
    IReadOnlyList<BenchmarkResult> Results);

sealed record BenchmarkOptions(
    IReadOnlyList<int> ActorCounts,
    int WarmupIterations,
    int Iterations,
    string? OutputPath,
    string? BaselinePath,
    double MaximumRegressionPercent)
{
    public static BenchmarkOptions Parse(string[] args)
    {
        var values = args.Select((value, index) => (value, index))
            .Where(pair => pair.value.StartsWith("--", StringComparison.Ordinal))
            .ToDictionary(pair => pair.value, pair => pair.index + 1 < args.Length ? args[pair.index + 1] : null,
                StringComparer.OrdinalIgnoreCase);
        var counts = values.GetValueOrDefault("--counts")?.Split(',')
            .Select(value => int.Parse(value, CultureInfo.InvariantCulture)).ToArray() ?? [100, 1_000, 5_000];
        var warmups = ParseInt(values, "--warmup", 3);
        var iterations = ParseInt(values, "--iterations", 20);
        var threshold = values.GetValueOrDefault("--max-regression-percent") is { } configured
            ? double.Parse(configured, CultureInfo.InvariantCulture)
            : 20d;
        if (counts.Length == 0 || counts.Any(value => value <= 0) || warmups < 0 || iterations <= 0 || threshold < 0)
            throw new ArgumentOutOfRangeException(nameof(args), "Counts and iterations must be positive; threshold cannot be negative.");
        return new(counts, warmups, iterations, values.GetValueOrDefault("--output"),
            values.GetValueOrDefault("--baseline"), threshold);
    }

    private static int ParseInt(IReadOnlyDictionary<string, string?> values, string key, int defaultValue) =>
        values.GetValueOrDefault(key) is { } configured
            ? int.Parse(configured, CultureInfo.InvariantCulture)
            : defaultValue;
}

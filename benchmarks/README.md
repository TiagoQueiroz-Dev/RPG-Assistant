# Simulation benchmarks

The benchmark exercises the real `NpcNeedsSimulationSystem` with deterministic in-memory worlds containing 100,
1,000 and 5,000 NPCs. Run it from the repository root in Release mode:

```powershell
dotnet run --project benchmarks/RpgWorld.Simulation.Benchmarks -c Release -- --output artifacts/simulation-baseline.json
```

Compare a later run with that baseline and fail with exit code `2` when the mean duration for any matching scale
regresses by more than 20%:

```powershell
dotnet run --project benchmarks/RpgWorld.Simulation.Benchmarks -c Release -- `
  --baseline artifacts/simulation-baseline.json --max-regression-percent 20
```

Use `--counts 100,1000,10000`, `--warmup 5`, or `--iterations 50` to change the workload. Keep the same machine,
runtime, build configuration, counts and iteration settings when comparing reports.

# Test suites

- `RpgWorld.Domain.Tests`, `RpgWorld.Application.Tests`, `RpgWorld.Simulation.Tests`, and `RpgWorld.Api.Tests`
  contain fast isolated tests.
- `RpgWorld.Simulation.Scenarios` contains deterministic multi-system world scenarios. It uses the shared
  `RpgWorld.Testing` clock helper and fixed `SeededSimulationRandom` seeds.
- `RpgWorld.Infrastructure.Tests` contains PostgreSQL integration tests and requires Docker through Testcontainers.

Run the deterministic simulation suites independently:

```powershell
dotnet test tests/RpgWorld.Simulation.Tests -c Release
dotnet test tests/RpgWorld.Simulation.Scenarios -c Release
```

The GitHub Actions workflow runs isolated tests, deterministic scenarios, PostgreSQL integration tests, the Angular
suite, and a simulation benchmark smoke run as separate jobs.

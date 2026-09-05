# Reference scenario

The repository includes an idempotent, seeded setup for development, demonstrations, integration tests, and
performance comparisons. It creates a 64 x 32 persisted map with two cities in separate regions, exactly 100 NPCs,
two factions with an initial trade relationship, eight discovered resource deposits, one player, and a running world
clock.
The river has a traversable crossing between the cities, and actor occupancy is initialized on the map tiles.

Set `ConnectionStrings__RpgWorld` and run:

```powershell
dotnet run --project src/RpgWorld.Api -- --seed-reference-scenario
```

To create a separate structural variant, provide a 32-bit integer seed:

```powershell
dotnet run --project src/RpgWorld.Api -- --seed-reference-scenario --scenario-seed=7331
```

Running the same seed again returns the existing scenario without duplicating it. The JSON output contains the world
and player identifiers. Use them to create the two authorization contexts:

- game master: claim `rpg:gm-world=<gameMasterWorldId>`;
- player: claims `rpg:world=<playerWorldId>` and `rpg:player-actor=<playerActorId>`.

On a normal API start, the Simulation Engine finds this world in the running state and processes its clock, NPCs,
resources, city economies, and faction systems. The game-master map exposes the full persisted world; the player map
uses the player's visibility and knowledge records, so it initially exposes only the area around Northwatch.

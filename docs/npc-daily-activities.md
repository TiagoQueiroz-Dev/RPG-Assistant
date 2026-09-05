# NPC daily activities

`Eat`, `Sleep` and `Work` execute through the per-NPC transactional action pipeline.
Utility AI can perceive discovered tile food deposits when personal food is absent.
No player command is needed. Each action resolves its site and uses the same A*
and validated movement service as `Travel`, advancing one tile per execution tick.
Arrival consumes the movement tick; activity time starts after arrival.

| Action | Source/site | Effect |
| --- | --- | --- |
| Eat | Personal `food`, `meal` or `ration` first; otherwise nearest discovered tile deposit yielding `food` with at least one unit | Consumes exactly one unit; reduces hunger by 35, after normal elapsed-time needs |
| Sleep | Home when assigned, otherwise current traversable tile; a referenced house must be completed and owned by the NPC | Recovers 25 energy per world hour, less normal fatigue; completes at 99 energy |
| Work | Active resident city center, with a supported job | One world hour of work transfers 2 money from city to NPC and costs 5 energy |

Farmers produce one food, lumberjacks one wood, miners one stone, and artisans one
tools unit in the city stock. Merchant, guard, healer, scholar, laborer and innkeeper
jobs perform paid services without material output. Insufficient city wealth fails
the shift without minting money or producing unpaid goods. More specialized
workplaces and module-defined job recipes are future extensions.

Sleep and work enforce the Utility AI minimum safety thresholds; work also requires
minimum energy. Missing food, invalid home, unsupported job, absent/destroyed city
or unreachable destination fails explicitly. Damage cancels the running action.
The next decision can choose a new action; search-budget exhaustion remains running
instead of claiming the destination is unreachable.

Inventory, deposit, city and NPC changes commit with execution state. Failures roll
back the entire step, including early movement saves. `LastProcessedAt` prevents
repeating effects at the same world instant. Utility AI also skips an already
processed instant, so a completed meal cannot restart when a whole tick is replayed.
Movement notifications are delivered after commit using the existing effect queue.

`NpcDailyActivityPostgreSqlTests.cs` exercises real decisions, pathfinding, movement,
fresh persistence scopes, duplicate ticks, missing sites/resources, unsafe conditions,
inventory preference, and interruption for all three actions. Rates use simulated
world time and therefore respect clock pause and speed.

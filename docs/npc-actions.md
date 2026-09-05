# NPC action execution

`NpcActor.ActionExecution` stores one current or most recently terminal action: execution ID, code, start/update times,
Running/Completed/Failed/Cancelled status, normalized progress, optional position and typed entity target, reason, and
last processed simulation instant. It is persisted as JSON and exposed in the NPC inspector. Routes may be rebuilt from
the saved target and the actor's authoritative position; the decision does not have to run again after a restart.

`SelectAction` defaults to retaining a running action when the same code is selected without a new target. A different
code or explicit different target cancels the old execution and starts a new ID. `KeepRunning` refuses replacement,
while `Restart` explicitly restarts even the same action. Selecting no action cancels the running action. An identical
decision can start a fresh execution after a terminal result. Invalid replacements are validated before cancellation.

Execution methods require the current execution ID, reject stale results and backwards time, and synchronize the legacy
`CurrentAction` property. Damage interrupts the action; death cancels it. Terminal states retain their target and reason.
`CanProcess` and `AdvanceAction` prevent replay of an already processed simulation instant. Executors must check before
applying effects and save effects with progress in the same transaction. Progress updates alone never complete an action;
the executor must explicitly finish it after validating the objective.

Lifecycle events record start, replacement cancellation, and terminal results in the World Event Log. Detailed progress
is available on the action snapshot without growing the persistent timeline for every tick.

## Execution pipeline

`NpcActionExecutionSimulationSystem` runs after Utility AI (order 35) at the movement cadence. Register an
`INpcActionExecutor` per action code through dependency injection. The executor receives the NPC, immutable execution
snapshot, simulation instant, and elapsed simulation time, and returns Continue, Complete, Fail, or Cancel.
The pipeline owns progression and terminal transitions; executors apply the action's world effects and may resolve targets.
The decision layer remains responsible only for selecting an action.

Each NPC step runs in its own database transaction. Exceptions, Fail, and Cancel roll back its changes, including early
saves; the pipeline then records a terminal result in a fresh transaction and continues the other NPCs. Host cancellation
rolls back and propagates without terminating the action, so the tick can be retried. Missing executors fail explicitly.
Executors should emit domain events through the persisted aggregates and leave delivery to the realtime infrastructure.
The latest execution outcome is available from `NpcActionExecutionDiagnostics` and structured logs.

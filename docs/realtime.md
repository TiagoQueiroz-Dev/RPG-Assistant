# Real-time world updates

The SignalR endpoint is `/hubs/world`. It publishes the `WorldUpdateMessage`
DTO; EF/domain entities are never serialized directly.

Supported subscription methods and groups are:

- `JoinWorld` / `LeaveWorld` → `world:{worldId}`;
- `JoinChunk` / `LeaveChunk` → `chunk:{chunkId}`;
- `JoinPlayer` / `LeavePlayer` → `player:{playerId}`;
- `JoinGameMaster` / `LeaveGameMaster` → `gm:{worldId}`.

Membership is checked against authenticated claims. World, chunk and GM access
use `rpg:world`, `rpg:chunk` and `rpg:gm-world`; player access uses the standard
name identifier or `sub`. An unauthenticated connection may reach the Hub but
cannot join a segmented group.

Clients should use automatic reconnect delays of 0, 2, 10 and 30 seconds. After
`onreconnected`, they must submit their current subscriptions again because a
full reconnect can receive a new connection ID. The server also enables
stateful reconnect support for short transport interruptions.

Internal producers depend on `IWorldUpdatePublisher` and can target a world,
chunk, player or game master without depending directly on SignalR.

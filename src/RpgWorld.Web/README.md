# RPG World Web

Angular 21 standalone frontend for the RPG Assistant.

```powershell
npm install
npm start
```

The development server runs at `http://localhost:4200` and expects the API at
`http://localhost:5169`. Environment-specific endpoints live under
`src/environments`.

Feature boundaries are intentional:

- `features/game-master` owns administrative routes and `WorldAdminView`;
- `features/player` owns player routes and `PlayerWorldView`;
- `core/api` centralizes HTTP and normalized failures;
- `core/realtime` centralizes SignalR, subscriptions and reconnect behavior.

The player feature must never import models from `features/game-master`.

# Campaigns

A campaign has its own ID, name, selected module, JSON session settings, creation time, and Active/Ended lifecycle.
Multiple campaigns may reference one persisted world. Ending a campaign preserves its metadata and the autonomous world.
Module IDs must resolve through the available module catalog when creating a campaign; world IDs have a database foreign key.

Game masters use these endpoints with an authenticated `rpg:gm-world` claim matching the route's world:

- `POST /api/worlds/{worldId}/campaigns`: `{ "name": "Evening game", "moduleId": "rpgworld.default", "settings": { "language": "pt-BR" } }`
- `GET /api/worlds/{worldId}/campaigns?offset=0&limit=50`: paged campaign metadata (maximum 100 entries).
- `GET /api/worlds/{worldId}/campaigns/{campaignId}`: metadata and session settings, without loading tiles, actors, or chunks.
- `POST /api/worlds/{worldId}/campaigns/{campaignId}/end`: idempotently end the campaign.

Settings must be a JSON object of at most 16384 characters; omitted settings default to `{}`. Invalid metadata or modules
return 400, missing worlds/campaigns return 404, and callers without the matching master context receive 403.
Campaign IDs are the stable association key for future participant and permission records. No participant access is granted
implicitly through the campaign record. Simulation parameters remain authoritative per world at `/simulation/settings`,
since campaigns referencing the same living world share its simulation. Session settings are independent for each campaign.

Apply the `Campaigns` database migration before using these endpoints.

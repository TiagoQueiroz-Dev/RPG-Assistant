# Redis cache

Redis is optional and is disabled by default. Enable it per environment with:

```powershell
$env:Redis__Enabled = 'true'
$env:Redis__ConnectionString = '<connection string from your secret store>'
```

`Redis__InstanceName` can isolate keys between deployments. The default prefix
is `rpg-world`.

The application contract is `ICacheService`. Cache-aside consumers must use
`GetOrLoadAsync` with a loader backed by PostgreSQL (the source of truth). A
missing, expired or temporarily unavailable Redis value therefore becomes a
cache miss and never makes durable world state unavailable.

Default absolute expirations are:

- active chunks: 5 minutes;
- sessions: 30 minutes;
- loaded entities: 10 minutes;
- read models: 2 minutes.

Explicit removal is available for invalidation after durable writes.

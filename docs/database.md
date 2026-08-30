# PostgreSQL

The API reads its database connection from the standard .NET configuration key
`ConnectionStrings:RpgWorld`. In deployed environments, set it with the
`ConnectionStrings__RpgWorld` environment variable or an environment-specific
configuration provider. Do not commit database passwords.

Development defaults for host, port, database and user live in
`appsettings.Development.json`; supply the password outside source control when
your local PostgreSQL requires one.

Create a migration from the repository root:

```powershell
dotnet tool restore
dotnet ef migrations add MigrationName --project src/RpgWorld.Infrastructure
```

Apply all pending migrations:

```powershell
$env:ConnectionStrings__RpgWorld = '<connection string from your secret store>'
dotnet ef database update --project src/RpgWorld.Infrastructure
```

The integration test starts an isolated PostgreSQL container, applies the real
migrations, writes a checkpoint and reads it back. A running Docker-compatible
daemon is required to execute that test.

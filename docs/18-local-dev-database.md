# Local Development Database

Use a local PostgreSQL instance for Windows development. The Raspberry Pi database is production-only and should not be used for day-to-day development.

## Recommended setup script

Run this from the repository root when you want to create or repair the local dev database:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/windows/dev/setup-local-postgres.ps1
```

The script:

- Prompts for the local PostgreSQL admin username, defaulting to `postgres`.
- Prompts for the local PostgreSQL admin password with `Read-Host -AsSecureString`.
- Does not store the PostgreSQL admin password.
- Creates or updates the local role `swedesevents_dev`.
- Creates the local database `swedeseventplanner_dev`.
- Stores the app connection string in .NET user-secrets for the API and Worker projects.
- Applies the existing EF Core migration.
- Verifies `/health` and `/health/ready` when it can start or reach the local API.

It does not touch Raspberry Pi production, staging, systemd, nginx, or production PostgreSQL.

Useful options:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/windows/dev/setup-local-postgres.ps1 -SkipHealthCheck
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/windows/dev/setup-local-postgres.ps1 -SkipMigrations
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/windows/dev/setup-local-postgres.ps1 -PsqlPath "C:\Program Files\PostgreSQL\18\bin\psql.exe"
```

## Create the dev database

The setup script above is preferred. If you need to do it manually, use local-only values:

```sql
CREATE USER swedesevents_dev WITH PASSWORD 'change-me-in-user-secrets';
CREATE DATABASE swedeseventplanner_dev OWNER swedesevents_dev;
\c swedeseventplanner_dev
ALTER SCHEMA public OWNER TO swedesevents_dev;
GRANT ALL ON SCHEMA public TO swedesevents_dev;
```

Use a throwaway local password. Do not commit real connection strings, database passwords, Temple API tokens, or production values.

## Store the connection string

Keep local secrets in .NET user secrets:

```powershell
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Port=5432;Database=swedeseventplanner_dev;Username=swedesevents_dev;Password=change-me-in-user-secrets" --project src\SwedesEventPlanner.Api
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Port=5432;Database=swedeseventplanner_dev;Username=swedesevents_dev;Password=change-me-in-user-secrets" --project src\SwedesEventPlanner.Worker
```

Enter passwords in your local terminal only. Do not paste real passwords into Codex chat or commit them to source control.

`appsettings.json` and `appsettings.Development.json` should contain only safe defaults and placeholders.

## Apply migrations locally

From the repository root:

```powershell
$env:ASPNETCORE_ENVIRONMENT = "Development"
$env:DOTNET_ENVIRONMENT = "Development"
dotnet ef database update --project src\SwedesEventPlanner.Infrastructure\SwedesEventPlanner.Infrastructure.csproj --startup-project src\SwedesEventPlanner.Api\SwedesEventPlanner.Api.csproj
```

The API may auto-apply migrations only in development when enabled by configuration. Production Pi migrations should be explicit deploy/operator steps added later.

# Local Development Database

Use a local PostgreSQL instance for Windows development. The Raspberry Pi database is production-only and should not be used for day-to-day development.

## Script Responsibilities

- `setup-local-postgres.ps1`: one-time create/repair for the local PostgreSQL role, database, user-secrets, and migrations. This script may ask for local PostgreSQL admin credentials.
- `update-local-database.ps1`: apply EF Core migrations to the existing local development database.
- `reset-local-database.ps1`: clear local app schema/data for manual testing, then apply EF Core migrations. This script connects with the configured app connection string and does not drop the database.

None of these scripts touch Raspberry Pi production, staging, systemd, nginx, or production PostgreSQL.

## Setup Or Repair

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
- Applies EF Core migrations unless `-SkipMigrations` is used.
- Verifies `/health` and `/health/ready` when it can start or reach the local API.

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

## Apply Migrations

From the repository root:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/windows/dev/update-local-database.ps1
```

The API may auto-apply migrations only in development when enabled by configuration. Production Pi migrations should be explicit deploy/operator steps added later.

## Reset For Manual Testing

To remove old local demo or manual test rows, reset only the local app schema/data:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/windows/dev/reset-local-database.ps1
```

The reset script:

- Reads the configured local app connection string from the API project user-secrets or `ConnectionStrings__DefaultConnection`.
- Connects to the existing `swedeseventplanner_dev` database as the app role.
- Drops and recreates the `public` schema, clearing app tables and EF migration history.
- Runs EF Core migrations afterward.
- Does not call `dotnet ef database drop`.
- Does not require PostgreSQL admin credentials.

If the database does not exist, run `scripts/windows/dev/setup-local-postgres.ps1`. If reset fails because local processes are holding locks, stop the API, Worker, pgAdmin query tabs, and other PostgreSQL clients, then retry.

After reset, create events, teams, signups, boards, tiles, rules, and Temple links manually through `/admin` or `src/SwedesEventPlanner.Api/SwedesEventPlanner.Api.http`. App-level demo seed helpers are intentionally not part of the manual workflow.

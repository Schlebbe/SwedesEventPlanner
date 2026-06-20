# Local Development Database

Use a local PostgreSQL instance for Windows development. The Raspberry Pi database is production-only and should not be used for day-to-day development.

## Create the dev database

Example SQL for a local machine:

```sql
CREATE USER swedesevents WITH PASSWORD 'change-me-in-user-secrets';
CREATE DATABASE swedeseventplanner OWNER swedesevents;
```

Use a throwaway local password. Do not commit real connection strings, database passwords, Temple API tokens, or production values.

## Store the connection string

Keep local secrets in .NET user secrets:

```powershell
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Port=5432;Database=swedeseventplanner;Username=swedesevents;Password=change-me-in-user-secrets" --project src\SwedesEventPlanner.Api
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Port=5432;Database=swedeseventplanner;Username=swedesevents;Password=change-me-in-user-secrets" --project src\SwedesEventPlanner.Worker
```

`appsettings.json` and `appsettings.Development.json` should contain only safe defaults and placeholders.

## Apply migrations locally

From the repository root:

```powershell
dotnet ef database update --project src\SwedesEventPlanner.Infrastructure\SwedesEventPlanner.Infrastructure.csproj --startup-project src\SwedesEventPlanner.Api\SwedesEventPlanner.Api.csproj
```

The API may auto-apply migrations only in development when enabled by configuration. Production Pi migrations should be explicit deploy/operator steps added later.

## Encoding

Preserve UTF-8 text, including Swedish characters such as å, ä, and ö.

If terminal output shows mojibake such as Ã¥, Ã¤, or Ã¶, assume it may be a shell display encoding issue. Verify file bytes as UTF-8 before replacing non-ASCII text.

Do not convert Swedish text to ASCII approximations.

## Raspberry Pi Access

This repo must be self-sufficient for Raspberry Pi diagnostics. Do not rely on scripts from `SwedesClanTracker` during future work.

Use the local scripts in this repo:

```text
scripts/windows/pi/test-pi-ssh.ps1
scripts/windows/pi/check-pi-db-readonly.ps1
scripts/windows/pi/inspect-pi-prereqs.ps1
```

Prefer these helpers and `scripts/windows/pi/pi-common.ps1` over plain `ssh`.

Plain `ssh user@host` can hang or fail because it skips the configured key, known_hosts file, `BatchMode=yes`, and timeout options.

Default Pi connection values:

```text
PI_HOST_OR_IP=192.168.10.106
PI_USER=sebastian
```

SSH key lookup order:

```text
PI_SSH_KEY_PATH, if set
~/.codex/keys/swedeseventplanner-pi/.codex_pi_ed25519
~/.codex/keys/swedesclantracker-pi/.codex_pi_ed25519
~/.ssh/id_ed25519
```

Known hosts lookup order:

```text
PI_SSH_KNOWN_HOSTS_PATH, if set
~/.codex/keys/swedeseventplanner-pi/.codex_known_hosts
~/.codex/keys/swedesclantracker-pi/.codex_known_hosts
~/.ssh/known_hosts
```

The fallback to the existing tracker key is intentional for the current shared Pi setup. Do not commit private keys, passwords, tokens, or real connection strings.

## Pi Diagnostics Guardrails

Safe read-only commands:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/windows/pi/test-pi-ssh.ps1 -NoPause
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/windows/pi/check-pi-db-readonly.ps1 -NoPause
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/windows/pi/inspect-pi-prereqs.ps1 -NoPause
```

Do not run deployment scripts unless the user explicitly asks.

For remote commands that need quotes, pipes, regexes, SQL, or multiline logic, prefer sending base64-encoded script/SQL content and decoding it remotely instead of building fragile nested shell quoting.

If SSH reports a transient transport error such as:

```text
kex_exchange_identification: read: Connection reset
```

retry once with the local Pi helper before treating it as an application failure.

For PostgreSQL diagnostics, prefer the read-only role `codex_ro` and base64-encoded SQL for anything beyond a trivial query.

`check-pi-db-readonly.ps1` defaults to the currently readable database `swedesclantracker` until the Event Planner database exists. Set `PI_READONLY_DATABASE=swedeseventplanner` after the Event Planner database and read-only access are created.

## Deployment Target

Primary deployment target:

```text
Raspberry Pi
PostgreSQL
systemd
nginx
ASP.NET Core serving the built React frontend in production
```

Operator/deployment scripts should be Windows PowerShell scripts run from the local Windows machine. They may execute remote Linux commands on the Pi through the local SSH helpers, but do not add Linux deployment scripts unless the user specifically asks for them.

Expected future app names and paths:

```text
system user: swedesevents
application root: /opt/swedeseventplanner
configuration root: /etc/swedeseventplanner
database: swedeseventplanner
database user: swedesevents
API service: swedeseventplanner-api
worker service: swedeseventplanner-worker
```

Do not hardcode local IPs, production URLs, database credentials, Linux paths, or secrets into application code.

Use:

```text
appsettings.json
appsettings.Production.json
environment variables
.env.example
systemd EnvironmentFile
deployment script variables
```

## Current Build Plan

Do not fork or build the RuneLite plugin first.

Use ASP.NET Core Minimal APIs with endpoint groups for the API. Keep endpoint handlers thin and put business logic in Application services.

Use a separate worker project/process from day one.

Use xUnit for .NET tests.

Start with:

```text
tests/SwedesEventPlanner.Domain.Tests
tests/SwedesEventPlanner.Application.Tests
```

Add Infrastructure, API, and PostgreSQL integration tests later. Use Testcontainers for PostgreSQL integration tests once the EF schema exists; manual DB checks are temporary only.

Use plain DTOs for successful API responses and ASP.NET Core ProblemDetails for errors. Do not invent a custom response envelope for MVP.

Use FluentValidation in application services.

Use BIGSERIAL/long IDs internally and text status/type columns with C# domain constants. Do not use UUID primary keys or PostgreSQL enums for MVP.

Use DateTimeOffset with PostgreSQL timestamptz. Store UTC and display in the configured event timezone, defaulting to Europe/Stockholm. Do not add NodaTime for MVP.

Keep EF Core migrations in SwedesEventPlanner.Infrastructure with the DbContext. The API may auto-apply migrations only in development behind configuration; production uses explicit deploy/script commands; the worker must not auto-apply migrations.

Enable Swagger/OpenAPI in development only. Do not expose it publicly in production by default.

Expose health endpoints as:

```text
/health for liveness
/health/ready for readiness/dependency readiness
```

Use Serilog for structured console/file logging on the Pi. Do not log secrets, tokens, connection string passwords, or raw sensitive payloads.

Use route groups:

```text
/api/events/... for public participant/event APIs
/api/admin/... for admin/testing APIs
/api/activity for mock/dev ingestion
```

Use React Router from the start.

Use TanStack Query from the start for frontend API state/caching.

Add shadcn/ui early for polished tables, dialogs, forms, cards, and common UI components.

Use npm for the React app.

Use hand-written typed frontend API functions for MVP. Do not generate a TypeScript client from OpenAPI yet.

Add Vitest and Testing Library early, but keep frontend tests light until UI structure settles.

Use `.editorconfig`, `dotnet format`, ESLint, and Prettier as the formatting/linting baseline.

Recommended solution layout:

```text
src/SwedesEventPlanner.Api
src/SwedesEventPlanner.Worker
src/SwedesEventPlanner.Domain
src/SwedesEventPlanner.Application
src/SwedesEventPlanner.Infrastructure
src/SwedesEventPlanner.Contracts
src/SwedesEventPlanner.Web
tests/...
```

Build in this order:

```text
solution skeleton
backend API
separate worker
clean ingestion API contract
TempleOSRS competition sync and cached external metrics
mock activity simulator
event processing
participant-facing UI
minimal admin/testing tools
deployment scripts
RuneLite plugin fork/build
```

The Valiance plugin is reference material for useful client-side data sources, not an API contract to preserve.

CSV event signup import is part of MVP.

Treat CSV import as event signup import, not general global player import. Google Forms/signup-specific fields such as availability, daily hours, preferred content, notes, and team preference belong on event signup or event participant records, not globally on players.

TempleOSRS competition rows still support player/account matching and admin review.

Do not auto-create every unmatched TempleOSRS name. Store unmatched identities for admin review with create, link, and competition-scoped ignore actions.

Scope external identity ignore to the specific external competition. Creating a player from Temple should use the Temple RuneScape name as the player's primary name and default display name.

Before implementing the TempleOSRS client, inspect TempleOSRS API docs/source and document the exact endpoints and response shapes being cached. The current provider contract lives in `docs/16-templeosrs-provider-contract.md`.

TempleOSRS is the source of truth for XP/KC gains and totals. Cached Temple-backed progress may increase or decrease after sync; do not maintain separate monotonic scoring values or infer/repair Temple data locally.

SwedesEventPlanner is the source of truth for event signups, local players, alt/account linking, local event teams before Temple export, event participation, board rules, scoring eligibility, and finalized roster/team export to TempleOSRS.

For MVP XP/KC team tiles, use cached Temple-returned team totals when the linked Temple competition is team-based. When the linked Temple competition is not team-based, use cached Temple-returned per-player gains grouped by local SwedesEventPlanner event teams.

Cache per-player gains for audit/debugging and cache Temple team members/totals when Temple returns them. Surface mismatches between Temple teams and local SwedesEventPlanner teams in admin/testing views.

Temple API tokens/competition keys are secrets. Store them outside committed config, use secret references where needed, and never log or return them in API responses.

Log Temple roster export attempts/errors separately from sync runs, using `external_competition_export_runs`.

Use external_competition_sync_runs as the Temple sync queue/state table, separate from activity_processing_queue.

Store Temple recalculation audit/contribution entries per affected tile/team per sync run, including decreases. Keep negative adjustments out of the main participant feed and visible in tile details/admin views.

For team-scoped events, known/matched players without a team assignment should not contribute progress until assigned.

Frontend style should feel like a clean esports scoreboard with subtle RuneScape/clan flavor: dark mode, team colors, progress bars, tile/tier cards, and a polished event feel.

## Validation Expectations

When code exists, run targeted validation before finishing:

```text
dotnet build
dotnet test
npm run build
npm run lint
```

Run only the commands that apply to the files changed and the current project state. If validation cannot run yet because the app has not been scaffolded, say so clearly.

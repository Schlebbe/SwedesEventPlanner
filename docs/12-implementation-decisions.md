# 12 - Implementation Decisions

This document records confirmed product and technical decisions for the first implementation.

## Technology stack

Backend:

```text
ASP.NET Core
Minimal APIs with endpoint groups
PostgreSQL
Entity Framework Core
FluentValidation
Separate background worker process
```

Frontend:

```text
React
TypeScript
Vite
Tailwind CSS
React Router
TanStack Query
shadcn/ui
Vitest
Testing Library
ESLint
Prettier
```

Add shadcn/ui early for polished tables, dialogs, forms, cards, and common UI components.

Use npm for frontend package management.

Use hand-written typed frontend API functions for MVP. Do not generate a TypeScript client from OpenAPI yet.

The UI should feel like a clean esports scoreboard with subtle RuneScape/clan flavor:

```text
dark mode
team colors
progress bars
tile/tier cards
polished event feel
```

## Architecture expectations

The application should be organized with clear boundaries between:

```text
domain logic
persistence
background processing
API endpoints
frontend UI
deployment and operations
```

Avoid:

```text
hardcoded event rules
duplicated helper methods
large god classes
hardcoded Linux paths
hardcoded local IPs
hardcoded database credentials
hardcoded production URLs
committed secrets
```

Use:

```text
appsettings.json
appsettings.Production.json
environment variables
.env.example with safe example values
EF Core migrations
structured logging
health checks where useful
deployment script variables
```

Rules should remain configurable through data, not hardcoded per event.

Use plain DTOs for successful API responses.

Use ASP.NET Core `ProblemDetails` for errors. Do not create a custom response envelope for MVP.

Use xUnit for .NET tests.

Start with separate test projects:

```text
tests/SwedesEventPlanner.Domain.Tests
tests/SwedesEventPlanner.Application.Tests
```

Add Infrastructure, API, and integration tests later after the EF schema and HTTP surface exist.

Use Testcontainers for PostgreSQL integration tests once the EF Core schema exists. Manual database testing is temporary only.

Add Vitest and Testing Library early for the React app, but keep frontend tests light until the UI stabilizes.

Use `BIGSERIAL`/long IDs internally. Do not use UUIDs for MVP.

Use text columns for statuses and types with C# domain constants. Avoid PostgreSQL enum types for MVP.

Use `DateTimeOffset` in .NET with PostgreSQL `timestamptz` for MVP. Store UTC and display in the configured event timezone. Do not add NodaTime yet.

Use FluentValidation in application services.

Enable Swagger/OpenAPI in development. Do not expose Swagger publicly in production by default.

The normal development environment is Windows local development. The Raspberry Pi is the production hosting target, not the day-to-day development environment, and there is no separate Pi staging environment planned for MVP.

Swagger should be used locally during Windows development and should not be exposed on the Pi production deployment by default.

Add health endpoints:

```text
/health = liveness
/health/ready = readiness/dependency readiness
```

Use Serilog for structured logging. Production on the Pi should log safely to console and file without logging secrets.

Use `.editorconfig` and `dotnet format` expectations for .NET formatting. Use ESLint and Prettier for the React/TypeScript app. Keep formatting and lint rules practical enough that they help the project stay consistent without creating noisy churn.

## Solution layout

Use a real `src` folder for application projects and a `tests` folder for test projects.

Recommended layout:

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

`src` is not just shorthand for the repository root. It should be an actual folder so production code, tests, docs, scripts, and deployment assets stay visually separated.

## API shape

Use ASP.NET Core Minimal APIs with endpoint groups for MVP.

Endpoint handlers should stay thin:

```text
parse route/body/query
call application service
map result to HTTP response
```

Do not put business logic directly inside endpoint lambdas.

Use Controllers later only if a feature grows large enough that controller conventions make it clearer.

Route conventions:

```text
/api/events/... = public participant/event APIs
/api/admin/... = admin/testing APIs
/api/activity = mock/dev ingestion only
```

## Worker shape

Use a separate worker project and process from day one.

The Pi deployment plan expects separate services:

```text
swedeseventplanner-api
swedeseventplanner-worker
```

The API and worker should share `Domain`, `Application`, `Infrastructure`, and `Contracts` projects.

EF Core migrations should live in `SwedesEventPlanner.Infrastructure` with the `DbContext` for MVP.

The API may auto-apply migrations in development only, behind development configuration.

Production should use explicit deployment/script commands for migrations.

The worker must not auto-apply migrations.

## Deployment target

The primary deployment target is a Raspberry Pi.

Environment split:

```text
Windows local development:
  primary coding/debugging loop
  run API locally
  run Worker locally
  run Vite frontend locally
  use local PostgreSQL or Testcontainers for integration tests
  use development config and manually created local test data
  enable Swagger/OpenAPI
  use safe fake/dev secrets only
  iterate quickly without touching production Pi state

Raspberry Pi production:
  hosted app environment
  run swedeseventplanner-api systemd service
  run swedeseventplanner-worker systemd service
  use production PostgreSQL database
  use nginx
  disable Swagger by default
  supply real Temple keys/secrets through production env files or systemd-managed secrets
  apply migrations through explicit deploy/script commands only
  never auto-migrate on normal production startup
  do not use for day-to-day development
```

Do not create a separate Pi staging/dev environment for MVP.

Do not create staging services, staging databases, staging nginx paths, or staging deployment scripts unless explicitly requested later.

Do not blur local development state and production Pi state.

The architecture should still be portable and should not assume Pi-specific paths, IP addresses, database names, credentials, or production URLs.

Local Windows PowerShell helpers live under `scripts/windows/pi` and should be used for Pi inspection and future deployment work. They were copied/adapted into this repository so this project does not depend on another repo for operations.

Do not add Linux deployment scripts unless a specific future need appears. Normal deployment operations should run from Windows and connect to the Pi over SSH/SCP/rsync through repository helper scripts.

## MVP UI

The first UI should include:

```text
participant-facing event and board views
minimal admin/testing area
active event display
team display
tile and tier progress
contribution logs
team score
board completion state
whether a team completed the board
```

The first UI should include enough admin/testing setup to create local smoke-test data manually:

```text
events
boards
teams
participants
rules
item groups
test players
```

Do not rely on app-level fake/demo seed helpers for MVP manual testing.

Include safe placeholders in `.env.example`, including:

```text
ADMIN_TOKEN=dev-admin-token-change-me
```

Testing tools can exist as simple admin pages or scripts rather than as a full admin product.

For MVP, a shared admin/testing token from an environment variable is enough for protected admin/test actions. Full user authentication can be added later.

Prefer:

```text
Authorization: Bearer <token>
```

Optionally accept this for simple scripts:

```text
X-Admin-Token: <token>
```

In production, ASP.NET Core should serve the built React frontend as static files. During development, the frontend can run separately through Vite.

Participant-facing pages can be public to anyone with the URL for MVP.

Public pages may show:

```text
teams
progress
contribution logs
player names
```

Public pages should hide:

```text
admin tools
raw payloads
secrets
private notes
internal errors
```

Temple metric contribution logs should show grouped team-level changes in the main UI. Per-player values should be available in tile detail views.

Negative Temple adjustments should be visible in tile details and admin/testing views, but should not be spammed into the main participant contribution feed.

Admin/testing tools should support manually assigning players to teams for MVP.

CSV event signup import is part of MVP.

CSV import should be scoped to event signups, not general global player import. Signup-specific fields such as availability, daily hours, preferred content, notes, and team preference belong on event signup or event participant records, not globally on `players`.

The intended setup workflow is documented in:

```text
docs/17-event-setup-workflow.md
```

## Multiple simultaneous events

The backend and database must support multiple simultaneous events from the start.

The first implementation should prove:

```text
multiple active events
a player participating in multiple events
one activity event evaluated against all relevant active events
event-specific progress
event-specific contributions
different teams for the same player in different events
```

The UI does not need advanced multi-event management in the MVP.

For team-scoped events, matched/known players who are not assigned to a team should not contribute progress until assigned. Surface unassigned matched players in admin/testing views.

## Event lifecycle and time

For MVP, event status is manually controlled.

Scoring requires all of:

```text
event.status = active
event_participants.status = active
activity.occurred_at >= event.starts_at
activity.occurred_at <= event.ends_at, when ends_at is set
```

Future work should add automatic status transitions based on event start/end times.

Store timestamps in UTC.

Display event times in the configured event timezone, defaulting to:

```text
Europe/Stockholm
```

Public event URLs should use unique slugs, such as:

```text
/events/summer-bingo-2026
```

Use long IDs internally.

## Bingo layouts

Bingo boards must support configurable layouts.

The category and tier layout is one supported board type, not the only possible bingo shape.

The system should support at least:

```text
category_tiers
grid
```

Do not hardcode all bingo events as three-tier category boards.

Grid boards should have configurable completion modes.

Planned completion modes:

```text
full_board
any_line
specific_number_of_lines
all_lines
custom
```

For MVP, support at least:

```text
full_board
any_line
```

The primary expected bingo mode is `full_board`, but the model and board completion service should not hardcode one meaning for all grid boards.

Tiers should be first-class goals with their own rules. A single tile may have tiers with different rule types.

Example:

```text
Slayer tier 1 = XP gained
Slayer tier 2 = XP gained
Slayer tier 3 = receive Imbued heart or Eternal gem
```

The backend and UI should distinguish:

```text
achieved = the tier condition happened
scored = the tier counts toward board completion and points
```

Tier progress is the public source of truth. Tile-level `current_value` exists only as a derived summary/compatibility field and must not mix unrelated units from tiers with different rule types or metric definitions.

## Board completion and scoring

Teams win by completing the board first.

If no team completes the board before the event ends, scoring determines the winner.

For MVP:

```text
each completed tile/tier gives points
highest score wins if no team completes the board
tie-breakers are deferred
```

For the three-tier category layout:

```text
each tier gives 1 point
same-metric tier targets are cumulative totals
team score is the sum of scored tier points
```

If a later tier condition is satisfied before earlier tiers are satisfied, the later tier can be marked as achieved, but it should not award score until the earlier tiers are also complete.

Example:

```text
Tier 3 objective: receive a Scythe of vitur
Team receives a Scythe while still missing tier 1 and tier 2
Tier 3 achievement is recorded
Tier 3 score is not awarded until tier 1 and tier 2 are complete
```

Later tier progress should still accumulate before earlier tiers score. When the earlier required tiers become scored, any already-achieved later required tiers should be recalculated and scored in order.

Category/tier boards are complete when all required tiers are scored, not merely achieved.

## XP and KC scoring source

For MVP, XP and KC event tiles should use TempleOSRS competition data as the source of truth.

```text
TempleOSRS competition gain remains the scoring source.
The backend syncs TempleOSRS competition results into cached database tables.
Rule evaluation reads cached database rows.
Page rendering reads cached database rows.
No direct TempleOSRS calls during rule evaluation or page rendering.
```

More specifically, `external_competition_metric` rule evaluation reads cached `external_competition_metrics` rows only.

Use `external_competition_metric` rules for XP/KC tiles.

Do not build plugin XP/KC scoring for MVP.

Before implementing the TempleOSRS client, inspect TempleOSRS API docs/source and document the exact endpoints and response shapes that will be cached.

Temple-backed XP/KC scoring should:

```text
store the latest Temple-returned gains and totals
score from cached Temple-returned gains
allow progress to increase or decrease when Temple data changes
avoid separate monotonic scoring values
avoid locally inferring or repairing Temple gains/totals
surface unmatched, missing, or anomalous rows in admin/testing views
```

For MVP XP/KC team tiles:

```text
team-based linked Temple competition:
  score from cached Temple-returned team totals

non-team linked Temple competition:
  score from cached Temple-returned per-player gains grouped by local SwedesEventPlanner event teams
```

Always cache per-player gains for audit/debugging.

Always cache Temple team members/totals when Temple returns them.

SwedesEventPlanner remains the source of truth for event signups, local players, alt/account linking, local event teams before Temple export, event participation, board rules, scoring eligibility, and finalized roster/team export to TempleOSRS.

TempleOSRS remains the source of truth for XP/KC competition gains, Temple-returned per-player gains, and Temple-returned team totals for team-based Temple competitions.

If a player is renamed, removed, or disappears from a Temple competition, fix that in TempleOSRS. After the next sync, this app's cached values should reflect TempleOSRS again.

The Temple sync worker should refresh linked competitions:

```text
periodically during active events, for example every 30 minutes
at event start
at event end
when manually triggered by an admin
when publicly requested from any event page and the competition is outside the public cooldown
```

Public TempleOSRS update button behavior:

```text
normal users can request a refresh from event/board pages
for MVP, refresh all linked competitions for that event
cooldown is 5 minutes per external competition
cooldown is per competition, not per user
spam prevention can be based on last accepted public sync request time
cooldown response should say when it was last successfully synced and how long remains
UI should show last successful sync time and next public refresh availability
```

Admins should have a force-sync action that bypasses the 5-minute public cooldown. Admin-triggered syncs should still be logged.

Admin force-sync must not create concurrent duplicate sync jobs for the same external competition. If a sync is already queued or running, return that state or record a skipped/already-running run instead of enqueueing a second worker job.

Failed syncs should be logged in `external_competition_sync_runs` and surfaced in admin/testing views.

## Mocked activity players

During MVP development, mocked activity may auto-create players.

This behavior must be behind a development/testing setting and should not be enabled unintentionally in production.

Production/plugin activity should reject unknown players.

Do not build a general global CSV player import for MVP.

Build CSV event signup import for MVP.

Players can also be matched or created from linked TempleOSRS competition results and imported signup rows.

Unmatched TempleOSRS rows should be stored with `runescape_name` and `player_id = null`, but should not count toward team/player progress until matched/imported.

Do not auto-create all unmatched TempleOSRS names.

Admin/testing tools should provide actions:

```text
Create player
Link to existing player
Ignore
```

`Ignore` is scoped to the specific external competition. It should not globally ignore that RuneScape name unless a later global-ignore feature is added.

Creating a player from a TempleOSRS row should use that Temple RuneScape name as `players.runescape_name` and the default `display_name`, then link the metric row to the new player.

Some players may have additional accounts that should count for their team. Admin/testing tools should make it quick to match alternate RuneScape names from Temple results to an existing player.

Future work can sync available TempleOSRS competitions for the clan ID and create event drafts or full events from them.

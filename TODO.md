# TODO

## After The Application Skeleton Exists

- Keep Windows local development as the only dev environment for MVP.
- Keep the Raspberry Pi as production hosting only; do not add Pi staging/dev scripts unless that decision changes later.
- Do not create staging services, staging databases, staging nginx paths, or staging deployment scripts unless explicitly requested later.
- Keep Pi scripts focused on read-only diagnostics, explicitly requested production deployment, production verification, and production service control after services exist.
- Create `deploy/env/api.env.example` with safe placeholder values, including `ADMIN_TOKEN=dev-admin-token-change-me`.
- Create `deploy/env/worker.env.example` if the worker is a separate process.
- Create `deploy/systemd/swedeseventplanner-api.service`.
- Create `deploy/systemd/swedeseventplanner-worker.service`.
- Create `deploy/nginx/swedeseventplanner.conf`.
- Decide nginx exposure before installing anything:
  - separate hostname
  - path under the existing host
  - different LAN port
  - replacement for the current default site
- Create Windows PowerShell database setup helper for the Pi.
- Create Windows PowerShell Pi service/nginx install helper if needed.
- Create `scripts/windows/pi/deploy-pi-stack.ps1` with `SupportsShouldProcess`.
- Create `scripts/windows/pi/deploy-pi-frontend.ps1` if frontend-only deploys are useful.
- Create `scripts/windows/pi/verify-pi-stack.ps1` once API/worker service names and health endpoints exist.
- Create service control helpers only after the service names exist:
  - `control-pi-api.ps1`
  - `control-pi-worker.ps1`
- Create journal helpers once services exist:
  - `get-pi-api-journal.ps1`
  - `get-pi-worker-journal.ps1`
- Create redacted env inspection helper after final env file names are known.
- Do not create Linux deployment scripts unless there is a specific future need; normal operation should run from Windows and deploy to the Pi over SSH/SCP/rsync.

## Application Work Still Needed

- Scaffold ASP.NET Core backend.
- Scaffold React + TypeScript + Vite + Tailwind frontend.
- Use npm for the React app.
- Add React Router.
- Add TanStack Query.
- Add shadcn/ui.
- Add hand-written typed frontend API functions.
- Do not generate a TypeScript client from OpenAPI for MVP.
- Add Vitest and Testing Library with light initial frontend tests.
- Install ESLint dependencies when the frontend package is scaffolded.
- Use Prettier for frontend/source formatting.
- Add PostgreSQL and EF Core migrations.
- Keep EF Core migrations in `SwedesEventPlanner.Infrastructure`.
- Add development-only API migration auto-apply behind configuration.
- Ensure worker never auto-applies migrations.
- Add explicit production migration deployment/script path.
- Add background worker service.
- Add TempleOSRS competition sync worker.
- Use `docs/16-templeosrs-provider-contract.md` as the TempleOSRS client contract.
- Store Temple competition ID and required key/secret reference safely outside committed config.
- Add Temple roster/team export workflow after local roster/team lock.
- Generate/push participants for non-team Temple competitions.
- Generate/push team assignments for team Temple competitions.
- Validate the linked Temple competition after export.
- Log Temple export attempts and errors in `external_competition_export_runs`.
- Keep Temple API tokens/competition keys out of logs, source control, `.env.example`, seed data, and API responses.
- Add health checks.
- Add `/health` liveness endpoint.
- Add `/health/ready` readiness/dependency endpoint.
- Add Serilog structured console/file logging without secrets.
- Add FluentValidation in application services.
- Enable Swagger/OpenAPI in development only.
- Keep manual local setup covered by admin/testing endpoints and `.http` examples.
- Add CSV event signup import from Google Forms exports.
- Store signup-specific fields on event signup/participant records, not on global players:
  - availability
  - daily hours
  - preferred content
  - notes
  - team preference
- Add TempleOSRS player import/matching from cached competition rows.
- Add external identity review actions:
  - Create player
  - Link to existing player
  - Ignore for a specific external competition
- Add quick admin/testing UI or script for manual team assignment.
- Surface matched players with no team assignment in admin/testing views.
- Add mock/dev simulator ingestion endpoint:
  - `POST /api/activity`
- Add activity processing queue.
- Add external competition tables.
- Add `external_competition_team_metrics` for cached Temple team totals.
- Add unique event slugs and public event slug routing.
- Add `external_competition_metric` rule type.
- Add cached TempleOSRS competition metric ingestion.
- Cache Temple-returned per-player gains for audit/debugging.
- Cache Temple-returned team members/totals for team competitions.
- Score XP/KC team tiles from Temple team totals when the linked Temple competition is team-based.
- Score XP/KC team tiles from per-player Temple gains grouped by local teams when the linked Temple competition is not team-based.
- Surface Temple/local team mismatches in admin/testing views.
- Add periodic Temple sync for active events.
- Add event start/end Temple sync triggers.
- Add public TempleOSRS update button with per-competition 5-minute cooldown.
- Add admin force-sync action and sync logging.
- Prevent concurrent duplicate sync jobs for the same external competition.
- Base UI freshness on last successful sync time.
- Base public sync spam prevention on last accepted public sync request time.
- Surface failed external competition sync runs in admin/testing views.
- Add first-class tile tiers.
- Add achieved vs scored tier state.
- Add board completion logic for:
  - `full_board`
  - `any_line`
- Add participant-facing event/board UI.
- Add minimal admin/testing UI or scripts.
- Add event timezone support, defaulting to Europe/Stockholm.
- Add xUnit test projects:
  - `tests/SwedesEventPlanner.Domain.Tests`
  - `tests/SwedesEventPlanner.Application.Tests`
- Add Testcontainers PostgreSQL integration tests after the EF schema exists.
- Use plain DTO success responses and ASP.NET Core ProblemDetails errors.
- Use DateTimeOffset with PostgreSQL timestamptz.

## Future Plugin Work

- Add clean plugin ingestion endpoints after the backend API contract is stable:
  - `POST /api/plugin/activity`
  - `POST /api/plugin/snapshot`
- Decide whether plugin XP/KC snapshots remain stats-only or become an optional future scoring source.

## Future Event Setup Work

- Add team draft workflow.
- Add a fuller manual event builder after MVP setup needs are clearer.
- Add TempleOSRS clan competition discovery by clan ID.
- Optionally create event drafts or full events from discovered TempleOSRS competitions.

## Do Not Do Yet

- Do not deploy to the Pi until the user explicitly asks.
- Do not overwrite the existing `SwedesClanTracker` nginx site.
- Do not fork/build the RuneLite plugin before the backend API contract is stable.
- Do not commit secrets, private keys, real tokens, or production connection strings.

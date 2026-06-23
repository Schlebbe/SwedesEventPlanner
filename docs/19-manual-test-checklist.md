# 19 - Manual Test Checklist

Use this checklist for Windows local development only. Do not deploy to the Raspberry Pi from this checklist.

## Health Checks

- Start the API in Development.
- Open `/health` and verify liveness returns healthy.
- Open `/health/ready` and verify PostgreSQL readiness is healthy.
- Open `/api` and verify the service information endpoint responds.

## Empty Local Database

- For a clean smoke test, run `scripts/windows/dev/reset-local-database.ps1` against the local development database only.
- Verify the reset clears local app data and reapplies migrations without secrets in command output.
- Do not use demo seed endpoints; app-level fake/demo seed data has been removed.

## Manual Event Setup

- Open `/admin`.
- Enter the local admin token from user-secrets or development config.
- Create a new event with status `draft`, `scheduled`, or `active`.
- Copy the returned slug/ID when using `src/SwedesEventPlanner.Api/SwedesEventPlanner.Api.http`.
- Verify `/events` is empty until an event is `scheduled` or `active`.

## Team Setup

- Open `/admin/events/{eventSlug}/setup`.
- Create at least two teams.
- Verify the participant list shows team counts and unassigned participants clearly.

## CSV Import

- Paste a small signup CSV with RuneScape name, availability, daily hours, preferred content, notes, and team preference.
- Import the CSV.
- Verify signups, players, and participants are created or updated.
- Verify signup-only fields are shown on signups and are not copied onto global players.

## Team Assignment

- Assign imported participants to teams.
- Clear one participant's team assignment and verify they become unassigned.
- Reassign the participant and verify they can contribute to team-scoped scoring.

## Board, Tile, Tier, And Rule Setup

- Create a board for the event.
- Create at least three tiles.
- Add one or more tiers to each tile.
- Create only one active rule per tier. Use separate tiles or tiers when testing different rule types.
- Create an `item_count` rule with `itemIds`, `requiredValue`, and `duplicatesCount`.
- Create a `point_threshold` rule with a `pointsTable` and `requiredValue`.
- Create an `external_competition_metric` rule with `provider`, `externalCompetitionId`, `metricType`, `metricKey`, and `requiredValue`.
- For a point tile, create multiple tiers that use the same points table with cumulative targets, such as 10, 25, and 50 points.
- For a mixed tile, create tiers with different rule types or item groups and verify each tier shows its own progress.
- If manual rules are supported by the current processing path, create a `manual` rule; otherwise leave it as setup data only for now.
- Verify `/api/admin/events/{eventSlug}/board-setup` shows board, tiles, tiers, and rules.

## Temple Read-Only Sync

- Create a dummy TempleOSRS competition outside this app.
- Link the Temple competition ID from the setup page or `.http` file.
- Trigger a read-only sync from the admin TempleOSRS panel.
- Verify sync runs, cached player metrics, cached team metrics, unmatched names, and team mismatch warnings appear.
- Verify no Temple create/add/remove/export endpoints are called or required.

## Manual Activity Simulation

- Use `POST /api/activity` in Development to submit manual activity payloads.
- Repeat a request with the same `dedupeKey`.
- Verify the second response is marked duplicate.
- `POST /api/activity` is a Development-only simulation endpoint for future plugin payloads. Real plugin endpoints are still future work.

## Worker Processing

- Start the worker with `scripts/windows/dev/run-local-worker.ps1`.
- Verify pending activity queue rows are processed.
- Refresh the public event board and confirm item/point progress updates.

## Public Event UI

- Open `/events`.
- Open the manually created event.
- Verify `/events/{eventSlug}` shows a team-first scoreboard overview with team cards and recent contributions.
- Open `/events/{eventSlug}/teams/{teamId}` from a team card.
- Verify the team board shows tile progress, tier progress, contribution feed, and TempleOSRS freshness.
- Verify point-threshold tiers show cumulative target progress, such as 14 / 25 after two 7-point drops.
- Verify later tier progress can appear before earlier tiers score, but the later tier is marked ready rather than scored until prerequisites score.
- Verify mixed-condition tiles display tier progress separately and do not show one combined mixed-unit progress value as the main tile progress.
- Submit activity and verify the public overview/team board update through automatic polling without a manual browser reload.
- Verify team-level Temple sync contributions with no player display safely.
- Verify Temple-backed tier progress does not overwrite item-count or point-threshold tier progress on the same tile.
- Verify negative Temple sync adjustments do not appear in the main public contribution feed.

## Public Refresh Cooldown

- On the public event page, click the Temple refresh button.
- Verify the response status is shown without requiring an admin token.
- Click again immediately.
- Verify the UI reports the cooldown/next refresh availability and does not start a duplicate sync.
- Wait until the configured cooldown expires, then verify a new refresh can be requested.

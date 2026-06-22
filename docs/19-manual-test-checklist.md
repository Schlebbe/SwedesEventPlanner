# 19 - Manual Test Checklist

Use this checklist for Windows local development stabilization passes. Do not deploy to the Raspberry Pi from this checklist.

## Health Checks

- Start the API in Development.
- Open `/health` and verify it returns healthy liveness.
- Open `/health/ready` and verify PostgreSQL readiness is healthy.

## Local DB/Migrations

- Run `scripts/windows/dev/update-local-database.ps1`.
- Verify EF migrations apply without secrets in command output.
- For a clean smoke test, run `scripts/windows/dev/reset-local-database.ps1` only against the local development database.

## Demo Seed

- Start the API in Development.
- Use the admin token from local configuration.
- POST `/api/admin/dev/seed-mock-activity-demo`.
- Verify the response includes `local-mock-activity-demo`.
- Open `/events/local-mock-activity-demo` and verify multiple teams, partial progress, completed progress, and TempleOSRS cached freshness appear.

## CSV Import

- Open `/admin/events/local-mock-activity-demo/setup`.
- Enter the local admin token.
- Paste a small signup CSV with RuneScape name, availability, daily hours, preferred content, notes, and team preference.
- Import the CSV.
- Verify signups, players, and participants are created or updated without copying signup-only fields onto global players.

## Team Assignment

- In the event setup page, create a team.
- Assign an imported participant to that team.
- Clear a participant's team assignment.
- Verify unassigned participant count updates.

## Mock Activity Ingestion

- POST one of the sample activity JSON payloads returned by the demo seed to `/api/activity`.
- Repeat with the same `dedupeKey`.
- Verify the second response is marked duplicate.

## Worker Processing

- Start the worker with `scripts/windows/dev/run-local-worker.ps1`.
- Verify pending activity queue rows are processed.
- Refresh the public event board and confirm progress updates.

## Public Event UI

- Open `/events`.
- Open the seeded demo event.
- Verify the scoreboard, teams, tile progress, recent positive contribution feed, and TempleOSRS freshness panel render.
- Verify negative Temple sync adjustments do not appear in the main public contribution feed.

## Temple Read-Only Sync

- Link a TempleOSRS competition ID from the setup page.
- Trigger a read-only sync from the admin TempleOSRS panel.
- Verify sync runs, cached player metrics, cached team metrics, unmatched names, and team mismatch warnings appear.
- Verify no Temple create/add/remove/export endpoints are called or required.

## Public Refresh Cooldown

- On the public event page, click the Temple refresh button.
- Verify the response status is shown without requiring an admin token.
- Click again immediately.
- Verify the UI reports the cooldown/next refresh availability and does not start a duplicate sync.
- Wait until the 5-minute cooldown expires, then verify a new refresh can be requested.

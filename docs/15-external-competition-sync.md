# 15 - External Competition Sync

XP and KC event tiles should remain supported.

For MVP, XP/KC scoring should use TempleOSRS competition data as the source of truth, not RuneLite plugin XP/KC snapshots.

```text
TempleOSRS competition gain remains the scoring source.
Rule evaluation reads cached external_competition_metrics only.
Page rendering reads cached database data.
No live TempleOSRS calls during rule evaluation.
No live TempleOSRS calls during page rendering.
```

## Source Model

An event can link one or more external competitions.

For MVP, the first provider is:

```text
templeosrs
```

For local MVP testing, the same external TempleOSRS competition ID may be linked to more than one local event. Revisit this before production use so ownership, duplicate sync load, and cross-event scoring expectations are explicit.

Each linked competition should store:

```text
provider
external competition ID
display name
metric type
metric key
last sync status
last successful sync time
last accepted public sync request time
next public sync availability
provider-specific config
secret reference for provider keys, never the secret value
```

Examples:

```text
Temple competition for Slayer XP gained
Temple competition for Zulrah KC gained
```

## Cached Metrics

Temple sync writes cached per-player competition results to the database.

Minimum cached fields:

```text
external competition
RuneScape name
matched player ID, when known
metric type: xp or kc
metric key: skill or boss name
start value, if provided
current value, if provided
gained value
rank, if provided
metric row last synced time
raw metadata
```

The application should keep unresolved Temple rows by RuneScape name even if no local player match exists yet.

Unresolved rows should not count toward team/player progress while `player_id` is null.

If a RuneScape name is matched to an existing player as an alternate account, future syncs should populate `player_id` for that row and the metrics can count for that player's team.

For MVP, TempleOSRS competition rows support player/account matching, but they do not replace CSV event signup import.

CSV event signup import is part of MVP and should create event signup/participant records before roster/team export to TempleOSRS.

Do not auto-create all unmatched Temple names. Store external identities and let admins:

```text
Create player
Link to existing player
Ignore
```

`Ignore` is scoped to the specific external competition, not global.

Creating a player from a Temple row should use that Temple RuneScape name as the new player's primary `runescape_name` and default `display_name`, then link the cached metric row to that player.

## Rule Type

Use rule type:

```text
external_competition_metric
```

The rule reads cached competition metric rows from `external_competition_metrics`.

Rule evaluation must not use live TempleOSRS responses, raw sync response blobs, or ad hoc HTTP calls. If data is missing or stale, the rule should evaluate from the cached rows that exist and let the UI/admin tooling indicate sync freshness.

Example config:

```json
{
  "provider": "templeosrs",
  "externalCompetitionId": 123,
  "metricType": "xp",
  "metricKey": "Slayer",
  "required": 5000000,
  "valueField": "gained_value"
}
```

For team bingo with a team-based linked Temple competition:

```text
team progress = cached Temple-returned team gained_value/totals
```

For team bingo with a non-team linked Temple competition:

```text
team progress = sum Temple-returned cached gained_value for known players on that local event team
```

Always cache per-player gains for audit/debugging.

Always cache Temple team members/totals when Temple returns them.

If the Temple team assignment differs from this app's local event teams, admin/testing views should surface the mismatch.

For player-scoped goals:

```text
player progress = Temple-returned cached gained_value for that event participant
```

TempleOSRS is the source of truth for XP/KC gains and totals.

The app should:

```text
store the latest gains and totals returned by Temple
score from cached Temple gains
allow progress to increase or decrease if Temple data changes
avoid maintaining separate monotonic scoring values
avoid inferring, repairing, or smoothing Temple values locally
surface unmatched/missing players or sync anomalies in admin/testing views
```

If a player is renamed, removed, or disappears from a Temple competition, fix that in TempleOSRS. After the next sync, cached values should reflect TempleOSRS again.

For team-scoped events, matched players without a team assignment should not contribute progress until assigned. Surface those rows in admin/testing views.

## Roster Export

SwedesEventPlanner owns the finalized event roster/team assignment before Temple export.

TempleOSRS owns XP/KC gains after sync.

After the local roster and teams are locked, the app should push the finalized roster to the linked Temple competition:

```text
participants for non-team Temple competitions
team assignments/structure for team Temple competitions
```

Store the Temple competition ID in the database. Store any Temple competition key/secret safely outside committed config, such as an environment variable, deployment secret file, or secret reference. Do not store Temple keys in source control, `.env.example`, seed data, logs, or public/admin JSON responses.

Export attempts and errors should be logged separately from sync runs.

Recommended table:

```text
external_competition_export_runs
```

Export should support:

```text
preview/dry-run summary
generate/push participants for non-team competitions
generate/push team assignments for team competitions
validate linked Temple competition after export
safe error logging
```

The official TempleOSRS docs show add/remove participant endpoints but do not document a full replace-roster endpoint. Verify add/remove/team-update behavior against a test competition before automating destructive reconciliation.

## Sync Worker

Add a Temple sync worker that refreshes linked Temple competitions.

Before implementing the Temple client, inspect TempleOSRS API docs/source and document the exact endpoints and response shapes this app will cache.

The initial provider contract is documented in:

```text
docs/16-templeosrs-provider-contract.md
```

Sync triggers:

```text
periodically during active events, for example every 30 minutes
at event start
at event end
manual admin trigger
public user trigger when cooldown allows
```

The worker should store sync attempts in `external_competition_sync_runs`.

`external_competition_sync_runs` is also the Temple sync queue/state table. Do not use `activity_processing_queue` for Temple sync jobs.

Sync attempts should record:

```text
competition
trigger type
who or what triggered it
accepted/requested time when useful
started time
completed time
status
rows read
rows changed
error message
safe raw response or metadata when useful
```

Recommended statuses:

```text
queued
running
succeeded
failed
skipped_cooldown
skipped_already_running
```

Failed syncs must be recorded with status `failed`, an error message, and safe diagnostic metadata where useful. Admin/testing views should surface recent failures so operators can tell the difference between stale-but-healthy data and stale-because-sync-is-failing data.

Only one queued or running sync job should exist per external competition. If a sync is already queued or running, another request for the same competition should not create a concurrent duplicate job.

## Public Refresh Button

Event and board pages should show a public TempleOSRS update button.

Normal users can press it.

For MVP, a public refresh request from any event page should attempt to refresh all linked external competitions for that event, while enforcing cooldown per competition.

The action should enqueue a sync only if the linked external competition has not had an accepted public sync request in the last 5 minutes.

Cooldown behavior:

```text
cooldown applies per external competition
cooldown does not apply per user
multiple users cannot spam TempleOSRS
spam prevention can be based on last accepted public sync request time
```

If the competition is still on cooldown, return a friendly message:

```text
last successful sync time
remaining cooldown duration
when the next public refresh is available
```

For the public button, freshness should mean the last successful sync time. The cooldown/next-availability calculation may be based on the last accepted public sync request time, so users cannot spam requests while a slow or failing sync is still being handled.

The UI should show:

```text
last successful sync time
next public refresh availability
current sync status
last failed sync message or status in admin/testing views
friendly cooldown message when applicable
```

## Admin Force Sync

Admins should have a force-sync action.

Admin force-sync:

```text
bypasses the public 5-minute cooldown
enqueues a sync
is logged in external_competition_sync_runs
records the triggering admin/test identity when available
```

Admin force-sync bypasses the public cooldown, but it must not create concurrent duplicate sync jobs for the same external competition. If a sync is already queued or running, return that status or log a `skipped_already_running` sync run instead of enqueueing another worker job.

## Event Processing Interaction

Temple sync is separate from raw activity ingestion.

Recommended flow:

```text
Temple sync completes
  -> cached external competition metrics are updated
  -> affected external_competition_metric rules are recalculated
  -> event_tile_tier_progress is updated
  -> event_progress_contributions records the visible delta/audit entry per affected tile/team/sync run
```

Store audit/contribution entries for decreases as well as increases. Keep negative adjustments out of the main participant contribution feed, but show them in tile details and admin/testing views.

Do not make event page rendering call TempleOSRS to fill missing values.

Do not make the rule engine call TempleOSRS to evaluate a rule.

Event and board pages should display freshness from `last_successful_sync_at` and cached `external_competition_metrics`. Failed sync details belong in admin/testing views, with a lightweight participant-facing status if needed.

## Plugin Relationship

Do not build plugin XP/KC scoring for MVP.

The RuneLite plugin may later send XP/KC snapshots for long-term stats tracking or backup diagnostics, but MVP event scoring for XP/KC should be Temple-backed.

## Future TempleOSRS Discovery

Future work can sync available TempleOSRS competitions for the clan ID and create event drafts or full events from them.

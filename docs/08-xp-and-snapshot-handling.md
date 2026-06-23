# 08 - XP/KC Source and Snapshot Handling

## Purpose

XP and KC goals require special handling because they are commonly based on differences between a starting value and a later value.

For MVP event scoring, XP/KC tiles should use cached TempleOSRS competition data as the source of truth.

Plugin XP/KC snapshots may still be useful later for long-term stats tracking, but they should not be the MVP scoring source.

TempleOSRS competition gain remains the scoring source for XP/KC event tiles.

Rule evaluation and page rendering must read cached database rows, not call TempleOSRS directly.

A future plugin-snapshot scoring mode has a major desync risk at event start. If a player has no earlier snapshot, they could appear to instantly contribute a large amount of XP when they first enable the activity source.

This document defines the MVP TempleOSRS source behavior and records future plugin snapshot baseline behavior to avoid that issue if plugin-snapshot scoring is added later.

## MVP source of truth

Use `external_competition_metric` rules for XP/KC tiles.

The Temple sync worker refreshes linked TempleOSRS competitions and stores cached per-player gains and Temple team totals in the database when Temple returns them.

The rule engine then reads cached `external_competition_metrics` rows only.

For MVP XP/KC team tiles:

```text
team-based linked Temple competition:
  use cached Temple-returned team totals as the scoring input

non-team linked Temple competition:
  use cached Temple-returned per-player gains grouped by local SwedesEventPlanner event teams
```

Always cache per-player gains for audit/debugging.

Always cache Temple team members/totals when Temple returns them.

Do not infer XP/KC gains locally.

This avoids:

```text
calling TempleOSRS during rule evaluation
calling TempleOSRS during page rendering
depending on the RuneLite plugin for XP/KC scoring
inventing our own XP/KC baseline logic for MVP bingo scoring
```

## Future snapshot types

Recommended snapshot types:

```text
xp_snapshot
kc_snapshot
full_player_snapshot
collection_log_snapshot
```

A full player snapshot can contain multiple categories of data:

```json
{
  "skills": {
    "Slayer": 42000000,
    "Woodcutting": 12000000,
    "Hunter": 18000000
  },
  "bossKc": {
    "Zulrah": 1250,
    "Vorkath": 850
  }
}
```

## Future snapshot timing

Recommended activity source behavior:

```text
On login:
  send full player snapshot or XP/KC snapshots

On logout:
  send full player snapshot or XP/KC snapshots

Optional while logged in:
  send periodic XP/KC snapshots every few minutes
```

The activity source should not need to know which events are active.

## Plugin snapshot baseline problem

Problem scenario:

```text
Event starts at 18:00.
Player had 40,000,000 Slayer XP at event start.
Player enables plugin at 22:00.
First received snapshot says 45,000,000 Slayer XP.
```

If the backend uses 0 or missing baseline, it may incorrectly count 45,000,000 XP or 5,000,000 XP that may have been gained before the event.

## Plugin snapshot baseline policy

For future plugin-snapshot XP/KC rules, each participant needs an event-specific baseline.

Recommended baseline source priority:

```text
1. Explicit locked event baseline.
2. Latest valid snapshot at or before event start.
3. First snapshot after event start, with progress locked to zero until that snapshot exists.
```

The important rule:

```text
A player's first snapshot after event start should become their baseline, not immediately count as progress.
```

This prevents instant XP contribution from players who had no pre-event snapshot.

## Baseline locking for future plugin-snapshot scoring

Create event-specific baseline records.

Suggested table:

```text
event_player_baselines
```

Suggested fields:

```sql
id BIGSERIAL PRIMARY KEY,
event_id BIGINT NOT NULL REFERENCES events(id),
player_id BIGINT NOT NULL REFERENCES players(id),
baseline_type TEXT NOT NULL,
baseline_key TEXT NOT NULL,
baseline_value BIGINT NOT NULL,
baseline_at TIMESTAMPTZ NOT NULL,
source_snapshot_id BIGINT NULL REFERENCES player_snapshots(id),
created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
metadata_json JSONB NOT NULL DEFAULT '{}'
```

Recommended constraint:

```sql
UNIQUE (event_id, player_id, baseline_type, baseline_key)
```

Examples:

```text
baseline_type = xp
baseline_key = Slayer
baseline_value = 42000000

baseline_type = kc
baseline_key = Zulrah
baseline_value = 1250
```

## Baseline creation strategy for future plugin-snapshot scoring

At event start, the backend can attempt to create baselines from existing snapshots.

For each participant and tracked XP/KC rule:

```text
Find latest snapshot at or before event start.
If found, create baseline from that snapshot.
If not found, mark baseline as pending.
```

However, the activity source should not need to know the event has started.

## Pending baseline behavior for future plugin-snapshot scoring

If no baseline exists when a snapshot arrives during an active event:

```text
Use that first snapshot as the baseline.
Do not award progress from that snapshot.
Future snapshots can count progress against it.
```

Example:

```text
Event starts: 18:00
No Slayer XP baseline exists for Player A
22:00 snapshot: 45,000,000 Slayer XP
Create baseline = 45,000,000
Award 0 progress
23:00 snapshot: 45,800,000 Slayer XP
Progress = 800,000
```

This is fairer than accidentally counting unknown pre-baseline progress.

## Strict event mode for future plugin-snapshot scoring

Some events may want stricter rules.

Event config can include:

```json
{
  "xpBaselinePolicy": "require_pre_event_snapshot"
}
```

Possible policies:

```text
first_snapshot_as_baseline
require_pre_event_snapshot
admin_locked_baseline
```

### first_snapshot_as_baseline

Default recommended policy.

If no pre-event snapshot exists, the first snapshot after event start becomes baseline and gives no progress.

### require_pre_event_snapshot

Strict policy.

If no pre-event snapshot exists, XP/KC goals do not progress until an admin resolves the baseline.

### admin_locked_baseline

Admin manually enters or approves baselines.

Useful for competitive or prize-heavy events.

## XP progress calculation for future plugin-snapshot scoring

For player-scoped XP:

```text
progress = current_xp - baseline_xp
```

For team-scoped XP:

```text
team progress = sum(max(0, player_current_xp - player_baseline_xp))
```

Do not add each snapshot as a contribution of full XP. Snapshots replace the current known value.

## Contribution strategy for future plugin snapshots

XP/KC snapshots are different from drops.

For item drops:

```text
Each matching drop adds progress.
```

For XP/KC snapshots:

```text
The snapshot updates calculated progress against a baseline.
```

The system can still create contribution records for audit purposes, but it must avoid double-counting.

Recommended approach:

```text
Store snapshot processing metadata.
Set affected tier progress to the recalculated value for that player/team.
Derive tile summary progress after tier scoring.
Optionally store delta from previous calculated progress as the contribution value.
```

Example:

```text
Previous calculated Slayer progress: 800,000
New calculated Slayer progress: 1,200,000
Contribution value added: +400,000
Tier progress: 1,200,000
```

## Full player snapshot on login for future stats

A login snapshot is useful because it gives the backend a recent state without event awareness.

Flow:

```text
Player logs in
  -> activity source sends full_player_snapshot
  -> backend stores snapshot
  -> processing worker checks if any active events need pending baselines
  -> if missing baseline exists, create it from this snapshot and award zero progress
```

## Full player snapshot on event start for future stats

The activity source should not need to send a snapshot because an event starts.

Instead, the backend should use existing data:

```text
latest valid snapshot before event start
```

If none exists, use the first later snapshot as baseline or require admin action depending on event policy.

## UI indicators

For MVP Temple-backed XP/KC rules, the website should show:

```text
linked TempleOSRS competition
last successful sync time
next public refresh availability
current cached team/player gained value
whether the latest sync failed
```

Participant-facing freshness should be based on the last successful sync. Failed sync details should be logged in `external_competition_sync_runs` and surfaced in admin/testing views.

For future plugin-snapshot XP/KC rules, the website may also show baseline status.

Baseline status examples:

```text
Ready: baseline locked
Pending: waiting for first snapshot
Invalid: required pre-event snapshot missing
Manual review: admin needs to set baseline
```

This prevents confusion during event start.

## Recommended MVP behavior

For MVP, use this policy:

```text
TempleOSRS competition gains through cached external_competition_metric rules
```

Meaning:

```text
Temple sync worker refreshes linked competitions.
Cached competition rows are stored in the database.
XP/KC tile rules read cached gained values.
Rule evaluation and page rendering never call TempleOSRS directly.
Plugin XP/KC snapshots are not used for event scoring.
```

Future plugin-snapshot scoring may use `first_snapshot_as_baseline` if needed, but it is not part of MVP XP/KC scoring.

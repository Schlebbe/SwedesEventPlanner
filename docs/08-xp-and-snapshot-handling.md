# 08 - XP and Snapshot Handling

## Purpose

XP and KC goals require special handling because they are based on differences between a baseline and a later snapshot.

A major risk is desync at event start. If a player has no earlier snapshot, they could appear to instantly contribute a large amount of XP when they first enable the activity source.

This document defines baseline behavior to avoid that.

## Snapshot types

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

## Snapshot timing

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

## Event baseline problem

Problem scenario:

```text
Event starts at 18:00.
Player had 40,000,000 Slayer XP at event start.
Player enables plugin at 22:00.
First received snapshot says 45,000,000 Slayer XP.
```

If the backend uses 0 or missing baseline, it may incorrectly count 45,000,000 XP or 5,000,000 XP that may have been gained before the event.

## Baseline policy

For XP/KC-based event rules, each participant needs an event-specific baseline.

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

## Baseline locking

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

## Baseline creation strategy

At event start, the backend can attempt to create baselines from existing snapshots.

For each participant and tracked XP/KC rule:

```text
Find latest snapshot at or before event start.
If found, create baseline from that snapshot.
If not found, mark baseline as pending.
```

However, the activity source should not need to know the event has started.

## Pending baseline behavior

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

## Strict event mode

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

## XP progress calculation

For player-scoped XP:

```text
progress = current_xp - baseline_xp
```

For team-scoped XP:

```text
team progress = sum(max(0, player_current_xp - player_baseline_xp))
```

Do not add each snapshot as a contribution of full XP. Snapshots replace the current known value.

## Contribution strategy for snapshots

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
Set current progress to recalculated value for that player/team.
Optionally store delta from previous calculated progress as the contribution value.
```

Example:

```text
Previous calculated Slayer progress: 800,000
New calculated Slayer progress: 1,200,000
Contribution value added: +400,000
Current progress: 1,200,000
```

## Full player snapshot on login

A login snapshot is useful because it gives the backend a recent state without event awareness.

Flow:

```text
Player logs in
  -> activity source sends full_player_snapshot
  -> backend stores snapshot
  -> processing worker checks if any active events need pending baselines
  -> if missing baseline exists, create it from this snapshot and award zero progress
```

## Full player snapshot on event start

The activity source should not need to send a snapshot because an event starts.

Instead, the backend should use existing data:

```text
latest valid snapshot before event start
```

If none exists, use the first later snapshot as baseline or require admin action depending on event policy.

## UI indicators

The website should show baseline status for XP/KC rules.

Examples:

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
first_snapshot_as_baseline
```

Meaning:

```text
If a player has a pre-event snapshot, use it.
If not, their first snapshot during the event becomes baseline and awards zero progress.
```

This avoids unfair instant XP/KC gains while keeping the system easy to use.


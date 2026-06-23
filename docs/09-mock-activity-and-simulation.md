# 09 - Mock Activity and Simulation

## Purpose

The first version should be testable without a RuneLite plugin.

Mocked activity should be enough to build and validate:

- Activity ingestion.
- Database writes.
- Queue processing.
- Simultaneous events.
- Rule evaluation.
- Progress updates.
- Board UI.
- Cached TempleOSRS competition metric handling.

## Mocking options

Recommended options:

```text
Postman
curl
small simulator script
manual `.http` requests
```

Manual `.http` requests are preferred for MVP smoke testing so the local database starts empty and the tester creates the exact event, roster, board, and rules under test.

## Mock/dev activity endpoint

Recommended mock/dev endpoint:

```http
POST /api/activity
```

`/api/activity` is for simulator, Postman, curl, and development testing only. It is not the future production RuneLite plugin endpoint.

## Example curl requests

### Item drop

```bash
curl -X POST http://localhost:5000/api/activity \
  -H "Content-Type: application/json" \
  -d '{
    "playerName": "Sebbe",
    "activityType": "item_drop",
    "source": "Theatre of Blood",
    "itemId": 22486,
    "itemName": "Scythe of vitur",
    "quantity": 1,
    "occurredAt": "2026-07-02T18:30:00Z"
  }'
```

### Future XP snapshot

```bash
curl -X POST http://localhost:5000/api/activity \
  -H "Content-Type: application/json" \
  -d '{
    "playerName": "Sebbe",
    "activityType": "xp_snapshot",
    "skill": "Slayer",
    "xp": 47000000,
    "occurredAt": "2026-07-02T22:10:00Z"
  }'
```

XP snapshots are useful for future plugin/stat tracking tests, but MVP XP tile scoring should use cached TempleOSRS competition metrics.

### Future KC snapshot

```bash
curl -X POST http://localhost:5000/api/activity \
  -H "Content-Type: application/json" \
  -d '{
    "playerName": "Sebbe",
    "activityType": "kc_snapshot",
    "bossName": "Zulrah",
    "kc": 1265,
    "occurredAt": "2026-07-02T22:10:00Z"
  }'
```

KC snapshots are useful for future plugin/stat tracking tests, but MVP KC tile scoring should use cached TempleOSRS competition metrics.

## Simulator goals

The simulator should prove that:

```text
Multiple players can send activity.
Multiple events can be active at once.
A player can participate in multiple events.
One activity can progress multiple events.
Players not signed up do not progress events.
Activity before event start does not progress events.
Activity after event end does not progress events.
Duplicate activity does not double-count.
Cached TempleOSRS competition metrics update XP/KC tier progress.
Team-based TempleOSRS competitions score from cached Temple team totals.
Non-team TempleOSRS competitions score from cached per-player gains grouped by local event teams.
Public TempleOSRS sync requests respect the per-competition cooldown.
Admin force-sync does not enqueue duplicate concurrent sync jobs for the same competition.
Failed TempleOSRS sync runs are logged and visible in admin/testing views.
```

## Suggested simulator data

Manual setup target:

```text
1 manually created bingo event
2-4 teams
imported CSV signups
manual board, tile, tier, and rule setup
```

Example players:

```text
Player001
Player002
...
Player100
```

Example active events:

```text
Summer Bingo
Weekend Raid Hunt
Skilling Week
```

## Example simulated activity

```text
Player001 gets Avernic defender hilt from Theatre of Blood
Player002 gets Scythe of vitur from Theatre of Blood
TempleOSRS cached Slayer competition row updates Player003 gained XP
Player004 gets Zulrah unique
Player005 gets pet
Player006 gets Soulreaper axe piece
```

## Example item pool

Start with a small hardcoded item pool.

```json
[
  {
    "source": "Theatre of Blood",
    "itemId": 22477,
    "itemName": "Avernic defender hilt",
    "activityType": "item_drop"
  },
  {
    "source": "Theatre of Blood",
    "itemId": 22486,
    "itemName": "Scythe of vitur",
    "activityType": "item_drop"
  },
  {
    "source": "Zulrah",
    "itemId": 12922,
    "itemName": "Tanzanite fang",
    "activityType": "item_drop"
  }
]
```

Exact item IDs can be corrected later. For MVP logic testing, consistency matters more than perfect OSRS data.

## Simulator behavior

A simple simulator loop:

```text
Every 1-3 seconds:
  pick random player
  pick random activity type
  generate payload
  POST /api/activity
```

For load testing:

```text
Generate 10,000 activity events quickly
Verify progress remains correct
Verify no duplicate contributions
Verify response times stay reasonable
```

## TempleOSRS competition metric tests

Non-team Temple competition test case:

```text
Event links Temple Slayer competition 123.
Team A includes Player003 and Player007.
Cached metric for Player003 gained Slayer XP = 3,000,000.
Cached metric for Player007 gained Slayer XP = 2,500,000.
Expected Team A Slayer progress: 5,500,000.
```

Team-based Temple competition test case:

```text
Event links team-based Temple Slayer competition 456.
Temple team "Blue" gained Slayer XP = 8,000,000.
Expected linked Blue team Slayer progress: 8,000,000.
Per-player gains are cached for audit/debugging, but the team tile uses the Temple team total.
```

Mismatch test case:

```text
Temple team "Blue" contains Player003.
Local SwedesEventPlanner team "Blue" does not contain Player003.
Expected result: admin/testing view surfaces the mismatch after validation/sync.
```

Another test case:

```text
Temple competition was synced 3 minutes ago.
Normal user presses update button.
Expected result: no sync enqueued, friendly cooldown message returned.
Message includes last successful sync time and roughly 2 minutes remaining.
```

Cooldown/spam-prevention test case:

```text
Public sync request was accepted 3 minutes ago but the sync failed.
Normal user presses update button.
Expected result: no sync enqueued, friendly cooldown message returned.
UI freshness still shows the last successful sync time, not the failed attempt time.
Admin/testing view shows the failed sync run.
```

Admin force-sync concurrency test case:

```text
Admin presses force-sync while a sync for the same competition is already queued or running.
Expected result: no duplicate worker job is created.
System returns the existing queued/running status or logs skipped_already_running.
```

## Postman collection

A Postman collection can include:

```text
Create activity: item drop
Create activity: duplicate event
Create activity: event outside time window
Link and sync cached Temple competition metric
Request public Temple sync while available
Request public Temple sync while on cooldown
Request admin force-sync
```

## Manual test checklist

This is not an implementation task list, but a set of behaviors the system should demonstrate:

```text
Scythe drop gives TOB points in multiple active events.
Drop from unsigned player creates activity but no event progress.
Duplicate Scythe payload does not double-count.
Temple cached XP/KC metrics update external_competition_metric tier progress.
Public Temple sync cooldown is per competition, not per user.
Admin force-sync bypasses cooldown and is logged.
Admin force-sync does not create duplicate concurrent sync jobs.
UI freshness uses last successful sync time.
Failed syncs are visible in admin/testing views.
Contribution history explains tile and tier progress.
```

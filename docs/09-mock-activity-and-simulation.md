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

## Mocking options

Recommended options:

```text
Postman
curl
small simulator script
backend seed script
```

A simulator script is preferred once the basic endpoint exists because it can generate many players and many events quickly.

## Activity endpoint

Recommended endpoint:

```http
POST /api/activity
```

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

### XP snapshot

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

### KC snapshot

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
XP baselines prevent instant unfair XP progress.
```

## Suggested simulator data

Seed:

```text
100 players
2 active bingo events
1 raid drop hunt event
4-8 teams per bingo event
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
Player003 gains Slayer XP snapshot
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

## XP baseline test

Important test case:

```text
Event starts at 18:00.
Player has no pre-event snapshot.
First snapshot at 20:00 says 50,000,000 Slayer XP.
Expected progress: 0.
Second snapshot at 21:00 says 50,500,000 Slayer XP.
Expected progress: 500,000.
```

Another test case:

```text
Pre-event snapshot at 17:50 says 40,000,000 Slayer XP.
Event starts at 18:00.
Snapshot at 20:00 says 42,000,000 Slayer XP.
Expected progress: 2,000,000.
```

## Postman collection

A Postman collection can include:

```text
Create activity: item drop
Create activity: XP snapshot
Create activity: KC snapshot
Create activity: duplicate event
Create activity: event outside time window
```

## Manual test checklist

This is not an implementation task list, but a set of behaviors the system should demonstrate:

```text
Scythe drop gives TOB points in multiple active events.
Drop from unsigned player creates activity but no event progress.
Duplicate Scythe payload does not double-count.
XP snapshot with no baseline creates baseline and gives zero progress.
Second XP snapshot gives only the gained amount.
Contribution history explains tile progress.
```


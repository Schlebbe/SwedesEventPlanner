# 10 - MVP Scope

## MVP definition

The MVP is a configurable event system that supports multiple simultaneous team-based bingo events using mocked player activity.

The purpose of the MVP is to prove the data model, rule engine, event processing, and progress UI before building a real RuneLite plugin.

## Included in MVP

### Players

The system should support known players with RuneScape names.

Account linking can be minimal or mocked.

### Events

The system should support multiple events existing at the same time.

Events should have:

```text
name
type
status
start time
optional end time
configuration
```

### Event teams

Teams should belong to a specific event.

A player can be on different teams in different events.

### Event participants

Players join events through event participation records.

Activity should only progress an event if the player is participating in that event.

### Bingo boards

A bingo event should have at least one board.

The first board can be category/tier based rather than strict 5x5.

### Tiles and rules

Tiles should have configurable rules.

Recommended initial rule types:

```text
item_count
point_threshold
xp_gained
kc_gained
manual
```

### Global activity log

All activity should be stored globally in `activity_events`.

Activity should not be tied to exactly one event.

### Processing queue

Activity should create processing jobs.

A worker should evaluate queued activity against all relevant active events.

### Progress and contribution tracking

The system should store:

```text
current tile progress
progress contribution history
```

Contribution history is required for auditability.

### Mock activity

Activity should be testable through:

```text
Postman
curl
simulator script
```

### XP/KC baseline handling

XP and KC goals should use event-specific baselines.

Default MVP policy:

```text
If no pre-event baseline exists, the first snapshot after event start becomes baseline and awards zero progress.
```

## Deferred from MVP

### Real RuneLite plugin

A real plugin is not part of the MVP.

The backend should be designed so a plugin can be added later as another activity source.

### Full account linking

Full account linking can be deferred.

A simple player table and mock names are enough for initial testing.

### Advanced anti-cheat

MVP can trust mocked input.

Future versions should consider:

```text
plugin tokens
signature validation
audit trails
admin review
rate limiting
```

### Advanced unlock chains

The database can include unlock condition tables, but complex unlock behavior can be deferred.

### Full collection log sync

Collection log support can be deferred or represented as mocked activity.

### Manual approval UI

Manual approval can be represented in the model, but a full admin approval UI can come later.

### Perfect OSRS item database

The MVP does not need a complete item database.

Use a small curated set of item IDs/groups for testing, then improve accuracy later.

## Recommended MVP rule coverage

The MVP rule types should cover most of the planned board:

| Board need | Rule type |
|---|---|
| Raid drop points | point_threshold |
| Wilderness drop points | point_threshold |
| Jars | item_count |
| Pets | item_count |
| Soulreaper axe pieces | item_count |
| GWD/Nex uniques | item_count or point_threshold |
| Skilling XP | xp_gained |
| Slayer XP | xp_gained |
| Boss kill goals | kc_gained |
| Unknown/special tiles | manual |

## MVP success criteria

The MVP should demonstrate these behaviors:

```text
One activity can progress multiple simultaneous events.
A player can have different teams in different events.
Unsigned players do not progress events.
Activity outside the event window does not progress events.
Drops add points/counts correctly.
XP snapshots use safe baselines.
Duplicate activity does not double-count.
Tile progress can be explained through contribution history.
```

## Non-goals

The MVP should avoid:

```text
Hardcoding one active event.
Making the activity source aware of bingo tiles.
Processing all progress only in frontend code.
Depending on a real plugin before the backend is proven.
Relying on polling every event for new database rows.
```


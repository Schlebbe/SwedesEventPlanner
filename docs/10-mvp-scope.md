# 10 - MVP Scope

## MVP definition

The MVP is a configurable event system that supports multiple simultaneous team-based bingo events using mocked player activity for drops/manual tests and cached TempleOSRS competition data for XP/KC tiles.

The purpose of the MVP is to prove the data model, rule engine, event processing, and progress UI before building a real RuneLite plugin.

## Included in MVP

### Players

The system should support known players with RuneScape names.

Account linking can be minimal or mocked.

CSV event signup import is part of MVP.

CSV import should be treated as event signup import, not as a general global player import.

Google Forms/signup-specific fields should live on event signup or event participant records, not globally on `players`.

Examples:

```text
availability
daily hours
preferred content
notes
team preference
```

Players can also be matched from TempleOSRS competition results when Temple sync discovers external names.

TempleOSRS rows with no matching local player should be stored but should not count toward progress until matched.

The MVP should support quick admin/testing matching for alternate RuneScape accounts so a player's additional accounts can count for their team once linked.

Unmatched TempleOSRS names should not be auto-created as players. Admin/testing tools should support creating a player, linking to an existing player, or ignoring the external identity for that specific external competition.

### Events

The system should support multiple events existing at the same time.

Events should have:

```text
slug
name
type
status
start time
optional end time
timezone
configuration
```

Event status is manually controlled for MVP, but scoring must still enforce the event start/end time window.

Automatic event status transitions are deferred.

Public event URLs use unique slugs, such as `/events/summer-bingo-2026`, with long IDs used internally.

### Event teams

Teams should belong to a specific event.

A player can be on different teams in different events.

For MVP, team assignment can be handled through a small admin/testing UI. A proper team draft is deferred.

For team-scoped events, known/matched players who are not assigned to a team should not contribute progress until assigned. Admin/testing views should surface unassigned matched players.

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
external_competition_metric
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

For Temple-backed XP/KC tiles, progress can increase or decrease when TempleOSRS cached gains change. TempleOSRS remains the source of truth.

### Mock activity

Activity should be testable through:

```text
Postman
curl
simulator script
```

### TempleOSRS-backed XP/KC tiles

XP and KC goals should remain supported, but MVP scoring should use cached TempleOSRS competition results.

The MVP should include:

```text
external_competition_metric rule type
TempleOSRS competition link per event/tile/rule as needed
Temple sync worker
cached external competition metrics in the database
public refresh button with per-competition cooldown
admin force-sync action
TempleOSRS-based player import/matching
TempleOSRS API endpoint/response documentation before implementing the Temple client
TempleOSRS finalized roster/team export after local roster lock
TempleOSRS export attempt/error logging
TempleOSRS membership/team validation after export
```

Rule evaluation and page rendering must use cached database data, never live TempleOSRS calls.

## Deferred from MVP

### Real RuneLite plugin

A real plugin is not part of the MVP.

The backend should be designed so a plugin can be added later as another activity source.

Plugin XP/KC scoring is not part of MVP.

### Full account linking

Full account linking can be deferred.

A simple player table and mock names are enough for initial testing.

### Team draft

A real team draft workflow can be deferred. MVP only needs manual team assignment.

### TempleOSRS clan competition discovery

Automatically syncing available TempleOSRS competitions from the clan ID and creating event drafts or full events can be deferred.

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
| Skilling XP | external_competition_metric |
| Slayer XP | external_competition_metric |
| Boss kill goals | external_competition_metric |
| Unknown/special tiles | manual |

## MVP success criteria

The MVP should demonstrate these behaviors:

```text
One activity can progress multiple simultaneous events.
A player can have different teams in different events.
Unsigned players do not progress events.
Unmatched TempleOSRS rows do not progress events until matched to a player.
Matched alternate accounts can count for the linked player's team.
Activity outside the event window does not progress events.
Drops add points/counts correctly.
XP/KC tiles use cached TempleOSRS competition gains.
Team-based TempleOSRS competitions score XP/KC team tiles from cached Temple-returned team totals.
Non-team TempleOSRS competitions score XP/KC team tiles from cached per-player gains grouped by local event team.
Rule evaluation and page rendering do not call TempleOSRS directly.
Temple metric corrections can increase or decrease progress.
Negative Temple adjustments appear in tile details/admin views rather than the main participant feed.
Public pages show teams, progress, contribution logs, and player names while hiding admin/internal data.
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

# 11 - Glossary

## Activity event

A raw fact about something a player did.

Examples:

```text
Player received item.
Player sent XP snapshot.
Player sent boss KC snapshot.
Player obtained collection log entry.
```

Stored globally in `activity_events`.

## Activity source

Something that sends activity to the backend.

Examples:

```text
Postman
curl
simulator script
future RuneLite plugin
```

## Baseline

A starting value for cumulative progress such as XP or KC.

Example:

```text
Player had 42,000,000 Slayer XP when the event started.
```

Progress is calculated as current value minus baseline value.

For MVP event scoring, XP/KC tiles use TempleOSRS competition gains instead of plugin snapshot baselines.

## Bingo board

A board belonging to a bingo event. It contains tiles.

A board may be a traditional grid or a category/tier layout.

## Contribution

An auditable progress entry explaining why a tile gained progress.

Example:

```text
Player A received Scythe of vitur, TOB tile +7 points.
```

Stored in `event_progress_contributions`.

## Event

A configured competition or activity period.

Examples:

```text
Summer Bingo
Raid Drop Hunt
Skilling Week
```

An event has participants, optional teams, start/end times, and rules.

Event timestamps are stored in UTC and displayed in the configured event timezone, defaulting to Europe/Stockholm.

For MVP, event status is manually controlled, but scoring still enforces the event start/end window.

## Event participant

A player who has joined a specific event.

Stored in `event_participants`.

## Event team

A team belonging to one event.

Teams are event-specific so a player can be on different teams in different events.

## External competition

A linked competition from an external provider such as TempleOSRS.

For MVP, external competitions provide cached XP/KC gains for event tiles.

## External competition metric

A cached per-player result from an external competition.

Examples:

```text
Player gained 5,000,000 Slayer XP in Temple competition 123.
Player gained 100 Zulrah KC in Temple competition 456.
```

Stored in `external_competition_metrics`.

Rows with no matched local player are stored for review but do not count toward progress until matched.

## Global activity

Activity stored independently of any one event.

A single global activity event can contribute to multiple active events.

## Item group

A reusable group of item IDs.

Examples:

```text
tob_uniques
jars
pets
soulreaper_axe_pieces
```

## Linked account

An external account identifier associated with a player.

For MVP, linked accounts are mainly useful for matching TempleOSRS RuneScape names, including alternate accounts, to the player whose team should receive credit.

## Point threshold

A rule where matching activities award points, and tiers are completed when point totals reach configured thresholds.

Example:

```text
Avernic defender hilt = 1 point
Scythe of vitur = 7 points
TOB tier 1 requires 10 points
```

## Progress

The current state of a tile or event goal.

Example:

```text
TOB tile: 18 / 25 points
Current tier: 1
```

Stored in `event_tile_progress`.

## Rule

Configuration that defines how activity becomes progress.

Examples:

```text
item_count
point_threshold
external_competition_metric
manual
```

## Rule engine

Backend component that evaluates activity against rule configuration.

## Snapshot

A point-in-time record of player state.

Examples:

```text
XP snapshot
KC snapshot
full player snapshot
collection log snapshot
```

Plugin XP/KC snapshots are future stats/tracking inputs, not the MVP scoring source for XP/KC event tiles.

## Tile

A bingo board goal.

Examples:

```text
TOB
Pets
Slayer XP
DT2
Jars
```

## Tier

A milestone within a tile.

Example:

```text
Pets tier 1 = 1 pet
Pets tier 2 = 3 pets
Pets tier 3 = 6 pets
```

## Unlock condition

A condition that must be satisfied before a tile can progress or be completed.

Example:

```text
Tile B unlocks after Tile A reaches tier 2.
```

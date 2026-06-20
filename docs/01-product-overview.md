# 01 - Product Overview

## Purpose

The platform is a modular clan event system for Old School RuneScape activity. It should collect player activity, store it as a global activity log, and use configurable event rules to calculate progress for one or more active clan events.

Bingo is the initial event format, but the architecture should not be limited to bingo. Future event types may include raid point races, drop hunts, skilling competitions, boss kill races, or clan-wide community goals.

## First implementation

The first implementation should include:

- A backend API.
- A database.
- A basic website for events, teams, boards, and progress.
- Mocked activity input through Postman, curl, or a simulator script.
- Support for multiple simultaneous events from the start.

A RuneLite plugin should be treated as a future activity source. The backend should not depend on the plugin existing during the MVP.

## Design goals

### Modular events

Events should be configurable. A bingo event, raid point event, or skilling event should use the same underlying concepts:

- Participants.
- Optional teams.
- Rules.
- Progress.
- Contributions.
- Start and end times.

### Simultaneous events

Multiple events may run at the same time. A player may participate in more than one event at once.

A single activity event can contribute to multiple active events independently.

Example:

```text
Player receives Scythe of vitur
  -> stored once in global activity log
  -> progresses Summer Bingo TOB tile
  -> progresses Raid Drop Hunt event
  -> appears in player drop history
```

### Event-agnostic activity source

The activity source should report facts only. It should not need to know which events are active.

Good:

```text
Player A received item X from source Y at time Z.
```

Bad:

```text
Player A completed bingo tile 12.
```

The backend is responsible for interpreting activity against event rules.

### Auditable progress

Progress should not only be stored as a final number. Each contribution should be stored so users can inspect how a tile reached its current value.

Example:

```text
TOB tile: 18 / 25 points

Contributions:
- Player A: Avernic defender hilt, +1
- Player B: Scythe of vitur, +7
- Player C: Ghrazi rapier, +3
```

## Core principle

```text
Plugin/mock sender reports facts.
Backend stores facts once.
Every active event evaluates those facts independently.
Progress is stored per event/team/tile.
```

## High-level modules

```text
Application
├── Activity ingestion
├── Activity storage
├── Event engine
├── Rule engine
├── Bingo/event UI
├── Player/team management
├── Mock activity simulation
└── Future RuneLite plugin integration
```

## MVP event types

The first version should primarily support team-based bingo, but the data model should allow other event types later.

Recommended initial event types:

```text
bingo
raid_drop_hunt
skilling_competition
manual_event
```

Only `bingo` needs a full UI in the first version.


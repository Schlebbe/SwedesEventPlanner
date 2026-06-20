# 02 - System Architecture

## Overview

The platform should separate activity collection from event evaluation.

```text
Mock sender / future RuneLite plugin
  ↓
Backend API
  ↓
Global activity log
  ↓
Activity processing queue
  ↓
Background worker
  ↓
Rule engine
  ↓
Event-specific progress
  ↓
Website UI
```

## Application boundaries

### Activity source

The activity source can be:

- Postman.
- curl.
- A simulator script.
- A future RuneLite plugin.

The source sends raw player activity to the backend. It does not decide whether a bingo tile or event goal has been completed.

### Backend API

The backend API receives activity, validates it, stores it, and enqueues it for processing.

The API should respond quickly. It should not perform expensive rule processing inside the request unless the system is still in a very early prototype stage.

### Database

The database stores:

- Players.
- Events.
- Event teams.
- Event participants.
- Global activity events.
- Snapshot data.
- Bingo boards and tiles.
- Tile rules.
- Progress contributions.
- Current progress.

### Background worker

The worker processes activity events from a queue.

For each activity event, it should:

1. Load the activity event.
2. Find all active event participations for that player.
3. Evaluate matching rules for each event.
4. Store progress contributions.
5. Update current progress.

### Rule engine

The rule engine takes:

- An activity event.
- An event participation.
- A rule configuration.
- Current event state.

It returns whether the activity matches the rule and how much progress should be awarded.

### Website

The website displays:

- Event list.
- Event detail pages.
- Team lists.
- Bingo boards.
- Tile progress.
- Contribution history.
- Player/team leaderboards.

For MVP, the website can poll the backend every few seconds. Real-time updates through WebSockets or Server-Sent Events can be added later.

## Data flow example

```text
1. Player receives Scythe of vitur.
2. Activity source sends POST /api/activity.
3. Backend inserts row in activity_events.
4. Backend inserts row in activity_processing_queue.
5. API returns success.
6. Worker picks up the queued activity.
7. Worker finds that the player is in two active events.
8. Rule engine evaluates each event's rules.
9. Summer Bingo TOB tile receives +7 points.
10. Raid Drop Hunt receives +7 points.
11. Website displays updated progress.
```

## Important boundaries

### The activity source should not be event-aware

Avoid making the future plugin fetch event configuration or know about active events.

The plugin should send data such as:

```json
{
  "playerName": "ExamplePlayer",
  "activityType": "item_drop",
  "source": "Theatre of Blood",
  "itemId": 22486,
  "itemName": "Scythe of vitur",
  "quantity": 1,
  "occurredAt": "2026-07-02T18:30:00Z"
}
```

The backend decides what this means.

### Events do not poll for changes

Events should not constantly query the database for new activity.

Instead:

```text
activity inserted -> processing job created -> worker evaluates active events
```

This avoids wasteful polling and scales better with multiple simultaneous events.

## Recommended MVP deployment shape

For local or Raspberry Pi hosting:

```text
Reverse proxy
Backend API
Background worker
PostgreSQL
Frontend
```

The worker can be part of the same backend process initially or a separate process later.

## Reliability model

The backend should store the activity and enqueue processing in the same transaction.

```text
Begin transaction
  insert activity_events
  insert activity_processing_queue
Commit transaction
```

This prevents activity from being stored without being processed.


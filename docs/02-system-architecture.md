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

XP/KC event tiles use a separate external competition sync path:

```text
Admin locks roster/teams
  ↓
Temple export
  ↓
TempleOSRS
  ↓
Temple sync worker
  ↓
Cached external competition metrics
  ↓
Rule recalculation
  ↓
Event-specific progress
  ↓
Website UI
```

Rule evaluation for XP/KC tiles must read cached `external_competition_metrics` only. Page rendering must use cached database data and should never call TempleOSRS directly.

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

Use ASP.NET Core Minimal APIs with endpoint groups. Endpoint handlers should be thin and delegate business logic to application services.

API route groups should be:

```text
/api/events/... for public participant/event APIs
/api/admin/... for admin/testing APIs
/api/activity for mock/dev ingestion
```

Use plain DTOs for successful responses and ASP.NET Core `ProblemDetails` for errors.

Use FluentValidation from application services rather than putting validation logic directly in endpoint handlers.

Swagger/OpenAPI should be enabled in development only and hidden from public production exposure by default.

### Database

The database stores:

- Players.
- External player identities.
- Event signups.
- Events.
- Event teams.
- Event participants.
- Global activity events.
- Snapshot data.
- External competition links.
- External competition export runs.
- Cached external competition metrics.
- External competition sync runs.
- Bingo boards and tiles.
- Tile rules.
- Progress contributions.
- Tier progress and derived tile summary progress.

Use EF Core migrations in `SwedesEventPlanner.Infrastructure` with the `DbContext`.

The API can auto-apply migrations in development only, behind explicit development configuration. The worker must not auto-apply migrations, and production migrations should run through explicit deploy/script commands.

### Background worker

The worker processes activity events from a queue.

For each activity event, it should:

1. Load the activity event.
2. Find all active event participations for that player.
3. Evaluate matching rules for each event.
4. Store progress contributions.
5. Update tier progress and derive tile summary progress.

The background worker should also include a Temple sync worker for linked external competitions.

The Temple sync worker refreshes cached TempleOSRS competition data:

```text
periodically during active events
at event start
at event end
when manually triggered by an admin
when publicly requested and the competition cooldown allows
```

The public cooldown applies per external competition, not per user. Spam prevention can be based on the last accepted public sync request time, while UI freshness should be based on the last successful sync time.

Admin force-sync bypasses the public cooldown, but it must not create concurrent duplicate sync jobs for the same external competition.

Temple export is an admin-triggered workflow, not page rendering. It pushes finalized local roster/team assignments to the linked Temple competition and logs attempts/errors separately from sync runs.

### Rule engine

The rule engine takes:

- An activity event.
- An event participation.
- A rule configuration.
- Current event state.

It returns whether the activity matches the rule and how much progress should be awarded.

For `external_competition_metric` rules, the rule engine reads cached external competition metrics from the database instead of a newly ingested activity event.

### Website

The website displays:

- Event list.
- Event detail pages.
- Team lists.
- Bingo boards.
- Tile and tier progress.
- Contribution history.
- Player/team leaderboards.
- TempleOSRS last successful sync time and next public refresh availability for linked competitions.
- Admin/testing views for unmatched external identities, team assignment, and sync failures.

For MVP, the website can poll the backend every few seconds. Real-time updates through WebSockets or Server-Sent Events can be added later.

Use React Router from the start.

## Data flow example

```text
1. Player receives Scythe of vitur.
2. Mock/dev activity source sends POST /api/activity.
3. Backend inserts row in activity_events.
4. Backend inserts row in activity_processing_queue.
5. API returns success.
6. Worker picks up the queued activity.
7. Worker finds that the player is in two active events.
8. Rule engine evaluates each event's rules.
9. Summer Bingo TOB tier progress receives +7 points.
10. Raid Drop Hunt receives +7 points.
11. Website displays updated progress.
```

`POST /api/activity` is the mock/dev endpoint for simulator and local testing. It should not become the production RuneLite plugin contract.

## TempleOSRS data flow example

```text
1. Admin creates a TempleOSRS Slayer competition.
2. Summer Bingo links the TempleOSRS competition ID/key/config.
3. Admin imports Google Forms CSV as event signups.
4. Admin matches players/alts and locks local roster/teams.
5. Backend exports finalized roster/teams to TempleOSRS.
6. Backend validates Temple membership/team assignment.
7. Temple sync worker refreshes the linked competition.
8. Backend stores cached per-player gains and Temple team totals.
9. Backend records the sync attempt.
10. Backend recalculates affected external_competition_metric rules.
11. Summer Bingo Slayer tier progress updates from cached gains/totals.
12. Website displays updated progress and the last successful sync time.
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

### TempleOSRS is not called from rendering or rule evaluation

Avoid:

```text
event page opens -> call TempleOSRS
rule evaluates -> call TempleOSRS
```

Use:

```text
sync trigger -> Temple sync worker -> cached metrics -> rules/UI read database
```

Failed Temple syncs should be recorded in `external_competition_sync_runs` and shown in admin/testing views.

## Recommended MVP deployment shape

For local or Raspberry Pi hosting:

```text
Reverse proxy
Backend API
Background worker
PostgreSQL
Frontend
```

The worker should be a separate process from day one so local development and Pi deployment match the expected API/worker service split.

## Reliability model

The backend should store the activity and enqueue processing in the same transaction.

```text
Begin transaction
  insert activity_events
  insert activity_processing_queue
Commit transaction
```

This prevents activity from being stored without being processed.

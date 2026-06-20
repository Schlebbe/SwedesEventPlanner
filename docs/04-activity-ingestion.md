# 04 - Activity Ingestion

## Purpose

Activity ingestion receives player activity from a mocked source initially and from a RuneLite plugin later.

The ingestion layer should store raw facts. It should not decide whether an event goal or bingo tile has been completed.

TempleOSRS competition sync is a separate external metric path, not activity ingestion. XP/KC scoring rules should read cached external competition rows for MVP.

## Mock/dev activity endpoint

Recommended MVP mock/dev endpoint:

```http
POST /api/activity
```

`/api/activity` is for development, simulator scripts, seed/test tooling, and local manual testing. It should not be treated as the production RuneLite plugin contract.

Example item drop payload:

```json
{
  "playerName": "Sebbe",
  "activityType": "item_drop",
  "source": "Theatre of Blood",
  "itemId": 22486,
  "itemName": "Scythe of vitur",
  "quantity": 1,
  "occurredAt": "2026-07-02T18:30:00Z"
}
```

Example XP snapshot payload:

```json
{
  "playerName": "Sebbe",
  "activityType": "xp_snapshot",
  "skill": "Slayer",
  "xp": 47000000,
  "occurredAt": "2026-07-02T22:10:00Z"
}
```

XP snapshot activity is for future plugin/stat tracking. MVP XP tile scoring should use cached TempleOSRS competition data instead.

Example KC snapshot payload:

```json
{
  "playerName": "Sebbe",
  "activityType": "kc_snapshot",
  "bossName": "Zulrah",
  "kc": 1265,
  "occurredAt": "2026-07-02T22:10:00Z"
}
```

KC snapshot activity is for future plugin/stat tracking. MVP KC tile scoring should use cached TempleOSRS competition data instead.

## Ingestion flow

```text
1. Receive activity payload.
2. Resolve player by RuneScape name.
3. Validate required fields for the activity type.
4. Generate or accept a dedupe key.
5. Insert into activity_events.
6. Insert into activity_processing_queue.
7. Return success.
```

The activity insert and queue insert should happen in the same transaction.

```text
Begin transaction
  insert activity_events
  insert activity_processing_queue
Commit transaction
```

## Activity types

Recommended initial activity types:

```text
item_drop
manual_test
```

Future activity types:

```text
xp_snapshot
kc_snapshot
full_player_snapshot
collection_log_entry
collection_log_snapshot
raid_completion
claimed_reward
chat_message_event
```

## Dedupe keys

A dedupe key prevents accidental duplicate activity inserts.

For mocked activity, this can be provided explicitly or generated from payload data.

Example dedupe key format:

```text
player:{playerId}:type:{activityType}:source:{source}:item:{itemId}:time:{occurredAt}
```

For future plugin activity, the plugin can send a stronger dedupe key if available.

The database should enforce uniqueness only when a dedupe key is present.

```sql
CREATE UNIQUE INDEX idx_activity_events_dedupe_key
ON activity_events (dedupe_key)
WHERE dedupe_key IS NOT NULL;
```

## Activity source should not know about events

The activity sender should not know about:

- Active events.
- Bingo tiles.
- Teams.
- Tile rules.
- Unlock conditions.

It should only report activity facts.

The backend decides which events the activity affects.

## Ingestion endpoint shape

The backend should support a mock/dev endpoint:

```http
POST /api/activity
```

Mocked activity and simulator tests should use this endpoint.

The future RuneLite plugin should use clean canonical plugin endpoints:

```text
POST /api/plugin/activity
POST /api/plugin/snapshot
```

Do not implement compatibility endpoints from the reference plugin.

Plugin endpoints should normalize payloads into the global activity model.

The plugin ingestion layer should:

```text
read player name from the plugin header
preserve the full raw payload
set source_system to runelite_plugin
set source_endpoint to the received endpoint
map plugin header time to occurred_at when no better activity timestamp exists
store received_at from the backend clock
extract repeated item or metric rows into child tables
enqueue activity processing in the same transaction
```

## Timestamp handling

Each activity event should store both:

```text
occurred_at
received_at
```

`occurred_at` is when the game activity happened.

`received_at` is when the backend received the event.

Event eligibility should usually be based on `occurred_at`.

## Validation examples

### item_drop

Required fields:

```text
playerName
activityType
itemId
quantity
occurredAt
```

Recommended fields:

```text
source
itemName
```

### xp_snapshot

Required fields:

```text
playerName
activityType
skill
xp
occurredAt
```

### kc_snapshot

Required fields:

```text
playerName
activityType
bossName
kc
occurredAt
```

## Error behavior

The API should reject malformed activity with a clear error.

Use ASP.NET Core `ProblemDetails` for error responses.

Examples:

```text
Unknown player
Unsupported activity type
Missing required field
Invalid timestamp
Duplicate dedupe key
```

Duplicates may return success if idempotency is preferred.

## Authentication

For the mock/dev `/api/activity` endpoint, a simple test token or local-only endpoint is enough.

Prefer:

```text
Authorization: Bearer <token>
```

`X-Admin-Token` can be accepted as an optional fallback for scripts.

Future plugin ingestion should use authentication, such as:

- Player-specific plugin token.
- Account linking flow.
- API key per plugin client.

Authentication should not be mixed with event logic.

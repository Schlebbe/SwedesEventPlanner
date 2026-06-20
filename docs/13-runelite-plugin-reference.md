# 13 - RuneLite Plugin Reference

The first RuneLite plugin will likely be forked from or heavily influenced by:

```text
https://github.com/Sicryption/ValianceClanPlugin
```

This document records useful data shapes from that plugin and what gaps we need to account for in the backend design.

This is not a backwards-compatibility contract. Do not implement the reference plugin's legacy endpoint paths.

## Reference plugin transport

The Valiance plugin sends JSON over HTTP using a configured server host, fixed `http://` protocol, fixed port `8080`, and plugin-specific legacy paths.

Every request adds a `header` object:

```json
{
  "player_name": "ExamplePlayer",
  "time": 1780000000000,
  "profile_type": "STANDARD"
}
```

The header time is generated when the plugin sends the request. It is not necessarily the exact game event time.

For our plugin fork, prefer a configurable full base URL and authentication token rather than a hardcoded protocol, port, or legacy path set.

Legacy endpoint paths from the reference plugin are intentionally omitted here. They are not API contracts to preserve for MVP.

The clean plugin API should be designed around canonical endpoints such as:

```text
POST /api/plugin/activity
POST /api/plugin/snapshot
```

## Current plugin payloads

### Item drops

Payload shape:

```json
{
  "item_drop": {
    "name": "Theatre of Blood",
    "quantity": 1,
    "items": {
      "22486": 1,
      "22477": 1
    }
  },
  "header": {
    "player_name": "ExamplePlayer",
    "time": 1780000000000,
    "profile_type": "STANDARD"
  }
}
```

Notes:

```text
item_drop.name is the loot source name from RuneLite LootReceived
item_drop.quantity is the source amount from RuneLite LootReceived
items is a map of item ID to quantity
item names are not sent
a single payload can contain multiple item IDs
```

Backend mapping:

```text
activity_type = item_drop
source = item_drop.name
raw_payload_json = full payload
activity_event_items = one row per item ID
```

A loot batch with multiple item IDs should become one `activity_events` row with many `activity_event_items` rows. Do not split one received payload into multiple activity rows.

### Boss kill and KC snapshot

Payload shape:

```json
{
  "boss_killed": "Zulrah",
  "prev_kc": 1264,
  "highscore": {
    "Zulrah": 1265,
    "Vorkath": 850,
    "Theatre of Blood": 42
  },
  "header": {
    "player_name": "ExamplePlayer",
    "time": 1780000000000,
    "profile_type": "STANDARD"
  }
}
```

Notes:

```text
boss_killed identifies the boss whose KC changed
prev_kc is the previous KC for that boss
highscore is a full boss KC map as known by the client
```

Backend mapping:

```text
activity_type = boss_kill_event or boss_kc_snapshot
boss_name = boss_killed
kc = highscore[boss_killed], when present
raw_payload_json = full payload
activity_event_metrics = one boss_kc metric per highscore entry
player_snapshots = optional boss_kc snapshot from the highscore map
```

Store both the discrete boss kill fact and the KC snapshot data when provided. Rules can use either depending on rule type:

```text
boss_kill_event for discrete kill events
boss_kc_snapshot for KC-delta goals
```

### Collection log snapshot

Payload shape:

```json
{
  "collection_log": {
    "22486": 1,
    "12013": 1
  },
  "header": {
    "player_name": "ExamplePlayer",
    "time": 1780000000000,
    "profile_type": "STANDARD"
  }
}
```

Backend mapping:

```text
activity_type = collection_log_snapshot
raw_payload_json = full payload
activity_event_items = one row per collection log item ID
player_snapshots = optional collection_log snapshot
```

Collection log ingestion should be stored and normalized if provided, but collection-log tile rules are not required before item drops, KC, and XP are working.

### New collection log entry

Payload shape:

```json
{
  "itemId": 22486,
  "header": {
    "player_name": "ExamplePlayer",
    "time": 1780000000000,
    "profile_type": "STANDARD"
  }
}
```

Backend mapping:

```text
activity_type = collection_log_entry
item_id = itemId
quantity = 1
raw_payload_json = full payload
```

### Combat achievements

Payload shape:

```json
{
  "combat_achievements": [1, 2, 3],
  "header": {
    "player_name": "ExamplePlayer",
    "time": 1780000000000,
    "profile_type": "STANDARD"
  }
}
```

Backend mapping:

```text
activity_type = combat_achievement_snapshot
raw_payload_json = full payload
activity_event_metrics = one combat_achievement metric per completed ID
player_snapshots = optional combat_achievement snapshot
```

Combat achievements should be stored if received, but combat-achievement rule support can be skipped for MVP unless planned event tiles require it.

## Database implications

The activity schema should not assume one activity row equals one item, one boss, or one metric.

Keep:

```text
activity_events as the durable raw envelope
raw_payload_json for full audit and replay
activity_event_items for repeated item rows
activity_event_metrics for repeated key/value state
player_snapshots for baselineable current player state
```

The existing convenience columns on `activity_events`, such as `item_id`, `boss_name`, and `kc`, are still useful for simple mocked activity and common queries, but plugin-derived batch payloads should use child rows.

## Current gaps

The inherited plugin does not currently provide:

```text
XP snapshots
skill XP maps
explicit event time separate from send time
strong dedupe keys
item names
authentication tokens
HTTPS by default
configurable full base URL
explicit payload schema version
explicit plugin version in each request
```

These gaps should be documented in code and added to our plugin fork where practical. XP and KC plugin snapshots may be useful for long-term player statistics or diagnostics, but MVP event scoring for XP/KC should use cached TempleOSRS competition data.

## Recommended changes for our plugin fork

Add request metadata:

```text
schemaVersion
pluginVersion
eventId or clientEventId for dedupe
occurredAt when available
sentAt
```

Add security:

```text
plugin token or player token
HTTPS-compatible base URL
avoid hardcoded production hostnames
```

Add missing activity:

```text
full player snapshot on login/logout
XP snapshot
KC snapshot
optional periodic snapshot
```

Improve item payloads:

```text
include item names where available
keep item IDs as the primary stable identifier
send one batch loot event but allow backend extraction into item rows
```

## Reference strategy

The backend should accept:

```text
mock/dev /api/activity payloads for simulator and tests
canonical /api/plugin/activity and /api/plugin/snapshot payloads for the future plugin
```

The Valiance plugin should influence useful data types and client-side collection methods, not force the backend to preserve inherited endpoint names.

Do not add legacy compatibility routes for the reference plugin.

All ingestion paths should normalize into the same global activity storage and queue processing model.

Do not fork or build the plugin as the first task. Build the backend, clean API contract, mock activity simulator, event processing, and UI first. Fork or build the plugin later once the ingestion contract is stable.

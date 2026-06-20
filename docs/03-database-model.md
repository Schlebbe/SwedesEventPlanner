# 03 - Database Model

## Principles

The database should support multiple simultaneous events from the start.

Important principles:

```text
Activity is global.
Events are separate from activity.
Progress is event-specific.
Teams belong to events.
Participants belong to events.
Rules belong to tiles or event goals.
Contributions are auditable.
```

## Core entities

```text
players
linked_accounts
activity_events
activity_processing_queue
player_snapshots

events
event_teams
event_participants

bingo_boards
bingo_tiles
tile_rules
tile_unlock_conditions

event_tile_progress
event_progress_contributions

item_groups
item_group_items
```

## Entity relationship overview

```text
players
  └── event_participants
        ├── events
        └── event_teams

players
  └── activity_events
        └── activity_processing_queue

 events
  └── bingo_boards
        └── bingo_tiles
              ├── tile_rules
              └── tile_unlock_conditions

 activity_events
  └── event_progress_contributions
        └── event_tile_progress
```

## players

Stores known website/plugin players.

Suggested fields:

```sql
id BIGSERIAL PRIMARY KEY,
display_name TEXT NOT NULL,
runescape_name TEXT NOT NULL,
created_at TIMESTAMPTZ NOT NULL DEFAULT now()
```

Recommended constraints:

```sql
UNIQUE (runescape_name)
```

## linked_accounts

Future-friendly table for account linking and plugin authentication.

Suggested fields:

```sql
id BIGSERIAL PRIMARY KEY,
player_id BIGINT NOT NULL REFERENCES players(id),
provider TEXT NOT NULL,
external_identifier TEXT NOT NULL,
created_at TIMESTAMPTZ NOT NULL DEFAULT now()
```

Examples of `provider`:

```text
runescape_name
plugin_token
discord
```

For MVP, this table can be skipped or created with minimal usage.

## events

Stores all event definitions.

Suggested fields:

```sql
id BIGSERIAL PRIMARY KEY,
name TEXT NOT NULL,
event_type TEXT NOT NULL,
status TEXT NOT NULL,
starts_at TIMESTAMPTZ NOT NULL,
ends_at TIMESTAMPTZ NULL,
created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
config_json JSONB NOT NULL DEFAULT '{}'
```

Examples of `event_type`:

```text
bingo
raid_drop_hunt
skilling_competition
manual_event
```

Examples of `status`:

```text
draft
scheduled
active
completed
cancelled
```

Recommended indexes:

```sql
CREATE INDEX idx_events_active_window
ON events (status, starts_at, ends_at);
```

## event_teams

Teams are scoped to one event.

Suggested fields:

```sql
id BIGSERIAL PRIMARY KEY,
event_id BIGINT NOT NULL REFERENCES events(id),
name TEXT NOT NULL,
created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
config_json JSONB NOT NULL DEFAULT '{}'
```

Recommended constraints:

```sql
UNIQUE (event_id, name)
```

## event_participants

Connects players to events and optionally to event-specific teams.

Suggested fields:

```sql
id BIGSERIAL PRIMARY KEY,
event_id BIGINT NOT NULL REFERENCES events(id),
player_id BIGINT NOT NULL REFERENCES players(id),
team_id BIGINT NULL REFERENCES event_teams(id),
joined_at TIMESTAMPTZ NOT NULL DEFAULT now(),
status TEXT NOT NULL DEFAULT 'active',
config_json JSONB NOT NULL DEFAULT '{}'
```

Recommended constraints:

```sql
UNIQUE (event_id, player_id)
```

Recommended indexes:

```sql
CREATE INDEX idx_event_participants_player
ON event_participants (player_id, event_id, status);
```

## activity_events

Global player activity log.

This table is not tied to one specific event.

Suggested fields:

```sql
id BIGSERIAL PRIMARY KEY,
player_id BIGINT NOT NULL REFERENCES players(id),
activity_type TEXT NOT NULL,
source TEXT NULL,
item_id INT NULL,
item_name TEXT NULL,
quantity INT NULL,
skill TEXT NULL,
xp BIGINT NULL,
boss_name TEXT NULL,
kc INT NULL,
occurred_at TIMESTAMPTZ NOT NULL,
received_at TIMESTAMPTZ NOT NULL DEFAULT now(),
raw_payload_json JSONB NOT NULL DEFAULT '{}',
dedupe_key TEXT NULL
```

Examples of `activity_type`:

```text
item_drop
xp_snapshot
kc_snapshot
collection_log_entry
collection_log_snapshot
raid_completion
full_player_snapshot
manual_test
```

Recommended indexes:

```sql
CREATE INDEX idx_activity_events_player_time
ON activity_events (player_id, occurred_at);

CREATE INDEX idx_activity_events_type_time
ON activity_events (activity_type, occurred_at);
```

Recommended constraint:

```sql
CREATE UNIQUE INDEX idx_activity_events_dedupe_key
ON activity_events (dedupe_key)
WHERE dedupe_key IS NOT NULL;
```

## activity_processing_queue

Database-backed queue for processing activity.

Suggested fields:

```sql
id BIGSERIAL PRIMARY KEY,
activity_event_id BIGINT NOT NULL REFERENCES activity_events(id),
status TEXT NOT NULL DEFAULT 'pending',
attempts INT NOT NULL DEFAULT 0,
available_at TIMESTAMPTZ NOT NULL DEFAULT now(),
locked_at TIMESTAMPTZ NULL,
processed_at TIMESTAMPTZ NULL,
error_message TEXT NULL,
created_at TIMESTAMPTZ NOT NULL DEFAULT now()
```

Recommended indexes:

```sql
CREATE INDEX idx_activity_processing_queue_pending
ON activity_processing_queue (status, available_at);
```

## player_snapshots

Stores point-in-time player state, such as XP, KC, or other baseline data.

Suggested fields:

```sql
id BIGSERIAL PRIMARY KEY,
player_id BIGINT NOT NULL REFERENCES players(id),
snapshot_type TEXT NOT NULL,
occurred_at TIMESTAMPTZ NOT NULL,
created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
snapshot_json JSONB NOT NULL
```

Examples of `snapshot_type`:

```text
xp
boss_kc
full_player
collection_log
```

Recommended indexes:

```sql
CREATE INDEX idx_player_snapshots_player_type_time
ON player_snapshots (player_id, snapshot_type, occurred_at);
```

## bingo_boards

A board belongs to one event.

Suggested fields:

```sql
id BIGSERIAL PRIMARY KEY,
event_id BIGINT NOT NULL REFERENCES events(id),
name TEXT NOT NULL,
rows INT NULL,
columns INT NULL,
created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
config_json JSONB NOT NULL DEFAULT '{}'
```

Rows and columns may be nullable if the first board style is list/category-based rather than strict grid-based.

## bingo_tiles

A tile belongs to a board.

Suggested fields:

```sql
id BIGSERIAL PRIMARY KEY,
board_id BIGINT NOT NULL REFERENCES bingo_boards(id),
title TEXT NOT NULL,
description TEXT NULL,
position_x INT NULL,
position_y INT NULL,
sort_order INT NOT NULL DEFAULT 0,
config_json JSONB NOT NULL DEFAULT '{}'
```

## tile_rules

Rules define how a tile progresses.

Suggested fields:

```sql
id BIGSERIAL PRIMARY KEY,
tile_id BIGINT NOT NULL REFERENCES bingo_tiles(id),
rule_type TEXT NOT NULL,
scope TEXT NOT NULL,
is_active BOOLEAN NOT NULL DEFAULT true,
config_json JSONB NOT NULL DEFAULT '{}',
created_at TIMESTAMPTZ NOT NULL DEFAULT now()
```

Examples of `rule_type`:

```text
item_count
point_threshold
xp_gained
kc_gained
manual
collection_log_obtained
claimed_reward
```

Examples of `scope`:

```text
player
team
event
```

## tile_unlock_conditions

Defines when a tile is available for progress.

Suggested fields:

```sql
id BIGSERIAL PRIMARY KEY,
tile_id BIGINT NOT NULL REFERENCES bingo_tiles(id),
condition_type TEXT NOT NULL,
config_json JSONB NOT NULL DEFAULT '{}',
created_at TIMESTAMPTZ NOT NULL DEFAULT now()
```

Example config:

```json
{
  "requiredTileId": 123,
  "requiredTier": 2
}
```

MVP can create this table without fully implementing advanced unlock logic.

## event_tile_progress

Stores current progress for a tile within an event.

Suggested fields:

```sql
id BIGSERIAL PRIMARY KEY,
event_id BIGINT NOT NULL REFERENCES events(id),
tile_id BIGINT NOT NULL REFERENCES bingo_tiles(id),
team_id BIGINT NULL REFERENCES event_teams(id),
player_id BIGINT NULL REFERENCES players(id),
current_value NUMERIC NOT NULL DEFAULT 0,
current_tier INT NOT NULL DEFAULT 0,
is_completed BOOLEAN NOT NULL DEFAULT false,
completed_at TIMESTAMPTZ NULL,
updated_at TIMESTAMPTZ NOT NULL DEFAULT now(),
metadata_json JSONB NOT NULL DEFAULT '{}'
```

For team bingo, `team_id` is set and `player_id` is null.

Recommended constraint:

```sql
UNIQUE (event_id, tile_id, team_id, player_id)
```

## event_progress_contributions

Auditable record of each activity contribution.

Suggested fields:

```sql
id BIGSERIAL PRIMARY KEY,
event_id BIGINT NOT NULL REFERENCES events(id),
tile_id BIGINT NOT NULL REFERENCES bingo_tiles(id),
rule_id BIGINT NOT NULL REFERENCES tile_rules(id),
team_id BIGINT NULL REFERENCES event_teams(id),
player_id BIGINT NOT NULL REFERENCES players(id),
activity_event_id BIGINT NOT NULL REFERENCES activity_events(id),
value_added NUMERIC NOT NULL,
description TEXT NULL,
created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
metadata_json JSONB NOT NULL DEFAULT '{}'
```

Recommended constraint:

```sql
UNIQUE (event_id, tile_id, rule_id, activity_event_id)
```

This prevents the same activity from being counted twice for the same tile/rule/event.

The same activity can still count for multiple events.

## item_groups

Reusable groups of item IDs.

Suggested fields:

```sql
id BIGSERIAL PRIMARY KEY,
key TEXT NOT NULL UNIQUE,
name TEXT NOT NULL,
created_at TIMESTAMPTZ NOT NULL DEFAULT now()
```

Examples:

```text
tob_uniques
cox_uniques
toa_uniques
soulreaper_axe_pieces
gwd_hilts
nex_uniques
jars
pets
barrows_items
moons_items
```

## item_group_items

Items belonging to a group.

Suggested fields:

```sql
id BIGSERIAL PRIMARY KEY,
item_group_id BIGINT NOT NULL REFERENCES item_groups(id),
item_id INT NOT NULL,
item_name TEXT NOT NULL,
metadata_json JSONB NOT NULL DEFAULT '{}'
```

Recommended constraint:

```sql
UNIQUE (item_group_id, item_id)
```


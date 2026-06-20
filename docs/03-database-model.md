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

Use `BIGSERIAL`/long IDs internally for MVP. Do not use UUID primary keys.

Use text columns for statuses and types, with C# domain constants. Avoid PostgreSQL enum types for MVP.

Use PostgreSQL `timestamptz` for instants and `DateTimeOffset` in .NET. Store UTC values.

## Core entities

```text
players
linked_accounts
external_player_identities
activity_events
activity_event_items
activity_event_metrics
activity_processing_queue
player_snapshots

events
event_teams
event_signups
event_participants
external_competitions
external_competition_export_runs
external_competition_player_reviews
external_competition_sync_runs
external_competition_metrics
external_competition_team_metrics

bingo_boards
bingo_tiles
bingo_tile_tiers
tile_rules
tile_unlock_conditions

event_tile_progress
event_tile_tier_progress
event_progress_contributions

item_groups
item_group_items
```

## Entity relationship overview

```text
players
  ├── linked_accounts
  ├── external_player_identities
  └── event_participants
        ├── events
        └── event_teams

events
  ├── event_signups
  └── external_competitions
        ├── external_competition_export_runs
        ├── external_competition_player_reviews
        ├── external_competition_sync_runs
        ├── external_competition_metrics
        └── external_competition_team_metrics

players
  └── activity_events
        ├── activity_event_items
        ├── activity_event_metrics
        └── activity_processing_queue

 events
  └── bingo_boards
        └── bingo_tiles
              ├── bingo_tile_tiers
              │     └── tile_rules
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

`runescape_name` can be the player's canonical/main account name for MVP.

Additional RuneScape accounts should be represented through `linked_accounts` so TempleOSRS competition rows for alts can be matched to the same player.

## linked_accounts

Future-friendly table for account linking and plugin authentication.

For MVP, this table is useful for matching TempleOSRS rows for alternate RuneScape accounts to an existing player.

Suggested fields:

```sql
id BIGSERIAL PRIMARY KEY,
player_id BIGINT NOT NULL REFERENCES players(id),
provider TEXT NOT NULL,
external_identifier TEXT NOT NULL,
display_name TEXT NULL,
created_at TIMESTAMPTZ NOT NULL DEFAULT now()
```

Examples of `provider`:

```text
runescape_name
templeosrs_runescape_name
plugin_token
discord
```

Recommended constraint:

```sql
UNIQUE (provider, external_identifier)
```

For MVP, create this table if alternate-account matching is included. Otherwise the schema should still be ready for it.

One RuneScape name should belong to only one player globally. Enforce this with the unique `players.runescape_name` constraint for primary names and the unique `(provider, external_identifier)` constraint for linked accounts.

## external_player_identities

Stores player identities discovered from external systems before, during, or after matching to local players.

For MVP, this table supports TempleOSRS admin review actions:

```text
Create player
Link to existing player
```

Suggested fields:

```sql
id BIGSERIAL PRIMARY KEY,
provider TEXT NOT NULL,
external_identifier TEXT NOT NULL,
display_name TEXT NOT NULL,
player_id BIGINT NULL REFERENCES players(id),
status TEXT NOT NULL DEFAULT 'unmatched',
first_seen_at TIMESTAMPTZ NOT NULL DEFAULT now(),
last_seen_at TIMESTAMPTZ NOT NULL DEFAULT now(),
reviewed_at TIMESTAMPTZ NULL,
reviewed_by TEXT NULL,
metadata_json JSONB NOT NULL DEFAULT '{}'
```

Examples of `provider`:

```text
templeosrs
```

Examples of `status`:

```text
unmatched
matched
```

Recommended constraint:

```sql
UNIQUE (provider, external_identifier)
```

Do not auto-create players for every unmatched external identity. Store the identity, keep `player_id` null, and let admins create or link it.

Ignore is scoped to a specific external competition, not global. Store competition-specific ignore state in `external_competition_player_reviews`.

## events

Stores all event definitions.

Suggested fields:

```sql
id BIGSERIAL PRIMARY KEY,
slug TEXT NOT NULL,
name TEXT NOT NULL,
event_type TEXT NOT NULL,
status TEXT NOT NULL,
starts_at TIMESTAMPTZ NOT NULL,
ends_at TIMESTAMPTZ NULL,
time_zone TEXT NOT NULL DEFAULT 'Europe/Stockholm',
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

For MVP, `status` is manually controlled. Scoring logic must still enforce `starts_at` and `ends_at`.

Future work can automatically transition status based on the event time window.

Recommended indexes:

```sql
CREATE INDEX idx_events_active_window
ON events (status, starts_at, ends_at);
```

Recommended constraints:

```sql
UNIQUE (slug)
```

Public event URLs should use slugs, such as `/events/summer-bingo-2026`, while internal references use long IDs.

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

## event_signups

Stores event-scoped signup rows imported from Google Forms or another signup source.

CSV signup import is part of MVP, but it is not a general global player import. Signup-specific fields belong here or on `event_participants.config_json`, not on `players`.

Suggested fields:

```sql
id BIGSERIAL PRIMARY KEY,
event_id BIGINT NOT NULL REFERENCES events(id),
player_id BIGINT NULL REFERENCES players(id),
runescape_name TEXT NOT NULL,
display_name TEXT NULL,
email_hash TEXT NULL,
availability_text TEXT NULL,
daily_hours NUMERIC NULL,
preferred_content TEXT NULL,
team_preference TEXT NULL,
notes TEXT NULL,
status TEXT NOT NULL DEFAULT 'imported',
source_system TEXT NOT NULL DEFAULT 'google_forms',
source_row_hash TEXT NULL,
submitted_at TIMESTAMPTZ NULL,
imported_at TIMESTAMPTZ NOT NULL DEFAULT now(),
metadata_json JSONB NOT NULL DEFAULT '{}'
```

Examples of signup-specific metadata:

```text
availability
daily hours
preferred content
team preference
notes
raw Google Forms column mapping
```

Recommended constraints:

```sql
UNIQUE (event_id, source_system, source_row_hash)
```

`email_hash` is optional and should only store a one-way hash if email dedupe is useful. Do not store unnecessary private signup data for MVP.

After import and matching, the app should create or update `players` and `event_participants` as appropriate.

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

For team-scoped events, active participants with `team_id = null` should not contribute progress until assigned to a team.

Recommended indexes:

```sql
CREATE INDEX idx_event_participants_player
ON event_participants (player_id, event_id, status);
```

## external_competitions

Stores external competition links used by event rules.

For MVP, TempleOSRS competitions are the source of truth for XP/KC gained tiles.

Suggested fields:

```sql
id BIGSERIAL PRIMARY KEY,
event_id BIGINT NOT NULL REFERENCES events(id),
provider TEXT NOT NULL,
external_id TEXT NOT NULL,
name TEXT NOT NULL,
metric_type TEXT NOT NULL,
metric_key TEXT NOT NULL,
competition_mode TEXT NOT NULL DEFAULT 'unknown',
secret_reference TEXT NULL,
status TEXT NOT NULL DEFAULT 'active',
last_synced_at TIMESTAMPTZ NULL,
last_successful_sync_at TIMESTAMPTZ NULL,
last_public_sync_request_accepted_at TIMESTAMPTZ NULL,
last_sync_status TEXT NULL,
last_sync_error TEXT NULL,
next_public_sync_available_at TIMESTAMPTZ NULL,
created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
config_json JSONB NOT NULL DEFAULT '{}'
```

Examples:

```text
provider = templeosrs
external_id = Temple competition ID
metric_type = xp
metric_key = Slayer
competition_mode = team
secret_reference = environment/config secret name, not the secret value

provider = templeosrs
external_id = Temple competition ID
metric_type = kc
metric_key = Zulrah
competition_mode = individual
```

Recommended constraint:

```sql
UNIQUE (provider, external_id)
```

Store Temple competition keys/secrets outside committed config. `secret_reference` may point to an environment variable, deployment secret file entry, or another safe secret provider. Do not store the actual Temple key in `config_json`, seed data, logs, or API responses.

Examples of `competition_mode`:

```text
unknown
individual
team
```

## external_competition_export_runs

Stores every attempt to push a finalized SwedesEventPlanner roster/team assignment to an external competition.

For MVP this is used for TempleOSRS participant/team export.

Suggested fields:

```sql
id BIGSERIAL PRIMARY KEY,
external_competition_id BIGINT NOT NULL REFERENCES external_competitions(id),
event_id BIGINT NOT NULL REFERENCES events(id),
trigger_type TEXT NOT NULL,
triggered_by TEXT NULL,
requested_at TIMESTAMPTZ NOT NULL DEFAULT now(),
started_at TIMESTAMPTZ NULL,
completed_at TIMESTAMPTZ NULL,
status TEXT NOT NULL,
participants_intended INT NULL,
participants_added INT NULL,
participants_removed INT NULL,
team_mappings_intended INT NULL,
error_message TEXT NULL,
request_summary_json JSONB NOT NULL DEFAULT '{}',
response_summary_json JSONB NULL,
validation_summary_json JSONB NULL,
metadata_json JSONB NOT NULL DEFAULT '{}'
```

Examples of `trigger_type`:

```text
admin_preview
admin_export
admin_validate
```

Examples of `status`:

```text
previewed
running
succeeded
failed
validation_failed
skipped_missing_secret
```

Do not store Temple keys/secrets in export run rows. Request/response summaries must be redacted and safe for admin diagnostics.

## external_competition_player_reviews

Stores competition-scoped review state for external identities discovered in a specific external competition.

This is where `Ignore` belongs for MVP. Ignoring a TempleOSRS name in one competition should not globally ignore that name for all future competitions.

Suggested fields:

```sql
id BIGSERIAL PRIMARY KEY,
external_competition_id BIGINT NOT NULL REFERENCES external_competitions(id),
external_player_identity_id BIGINT NOT NULL REFERENCES external_player_identities(id),
status TEXT NOT NULL DEFAULT 'unreviewed',
ignored_at TIMESTAMPTZ NULL,
reviewed_at TIMESTAMPTZ NULL,
reviewed_by TEXT NULL,
metadata_json JSONB NOT NULL DEFAULT '{}'
```

Examples of `status`:

```text
unreviewed
ignored
resolved
```

Recommended constraint:

```sql
UNIQUE (external_competition_id, external_player_identity_id)
```

## external_competition_sync_runs

Stores every external competition sync attempt.

Admin-triggered syncs should be logged even when they bypass cooldowns.

Failed syncs should be logged here and surfaced in admin/testing views.

Only one queued or running sync should exist for the same external competition at a time.

Suggested fields:

```sql
id BIGSERIAL PRIMARY KEY,
external_competition_id BIGINT NOT NULL REFERENCES external_competitions(id),
trigger_type TEXT NOT NULL,
triggered_by TEXT NULL,
requested_at TIMESTAMPTZ NULL,
started_at TIMESTAMPTZ NOT NULL DEFAULT now(),
completed_at TIMESTAMPTZ NULL,
status TEXT NOT NULL,
rows_read INT NULL,
rows_changed INT NULL,
error_message TEXT NULL,
raw_response_json JSONB NULL,
metadata_json JSONB NOT NULL DEFAULT '{}'
```

Examples of `trigger_type`:

```text
periodic
event_start
event_end
public_button
admin_force
manual_admin
```

Examples of `status`:

```text
queued
running
succeeded
failed
skipped_cooldown
skipped_already_running
```

Recommended active-job guard:

```sql
CREATE UNIQUE INDEX idx_external_competition_sync_runs_one_active
ON external_competition_sync_runs (external_competition_id)
WHERE status IN ('queued', 'running');
```

## external_competition_metrics

Stores cached per-player competition results from TempleOSRS.

Rule evaluation for `external_competition_metric` must read only these cached rows, never call TempleOSRS directly.

Page rendering must also read cached database rows and must not call TempleOSRS directly.

Suggested fields:

```sql
id BIGSERIAL PRIMARY KEY,
external_competition_id BIGINT NOT NULL REFERENCES external_competitions(id),
external_player_identity_id BIGINT NULL REFERENCES external_player_identities(id),
external_competition_player_review_id BIGINT NULL REFERENCES external_competition_player_reviews(id),
player_id BIGINT NULL REFERENCES players(id),
runescape_name TEXT NOT NULL,
metric_type TEXT NOT NULL,
metric_key TEXT NOT NULL,
start_value BIGINT NULL,
current_value BIGINT NULL,
gained_value BIGINT NOT NULL DEFAULT 0,
rank INT NULL,
last_synced_at TIMESTAMPTZ NOT NULL,
metadata_json JSONB NOT NULL DEFAULT '{}'
```

Recommended constraints:

```sql
UNIQUE (external_competition_id, runescape_name, metric_type, metric_key)
```

`player_id` should be populated when the TempleOSRS row can be matched to a known player. Keep `runescape_name` for auditability and for unresolved rows.

Rows with `player_id = null` should be stored but must not count toward team/player progress.

If a Temple row matches a linked alternate account, populate `player_id` with the owning player so the alt can count for that player's team.

Competition-ignored identities should remain visible in admin/testing views but should not count toward progress for that external competition.

## external_competition_team_metrics

Stores cached TempleOSRS team totals for team-based Temple competitions.

For MVP XP/KC team tiles, when the linked TempleOSRS competition is team-based, rule evaluation should use these cached Temple-returned team totals as the primary scoring input.

Suggested fields:

```sql
id BIGSERIAL PRIMARY KEY,
external_competition_id BIGINT NOT NULL REFERENCES external_competitions(id),
local_team_id BIGINT NULL REFERENCES event_teams(id),
temple_team_key TEXT NOT NULL,
team_name TEXT NOT NULL,
metric_type TEXT NOT NULL,
metric_key TEXT NOT NULL,
start_value BIGINT NULL,
current_value BIGINT NULL,
gained_value BIGINT NOT NULL DEFAULT 0,
rank INT NULL,
mvp_runescape_name TEXT NULL,
members_json JSONB NOT NULL DEFAULT '[]',
last_synced_at TIMESTAMPTZ NOT NULL,
metadata_json JSONB NOT NULL DEFAULT '{}'
```

Recommended constraints:

```sql
UNIQUE (external_competition_id, temple_team_key, metric_type, metric_key)
```

`local_team_id` should be populated when the Temple team maps cleanly to a SwedesEventPlanner event team. If it does not map cleanly, keep the Temple row cached and surface the mismatch in admin/testing views.

Per-player Temple rows should still be cached in `external_competition_metrics` for audit/debugging even when the team total is the scoring source.

## activity_events

Global player activity log.

This table is not tied to one specific event.

Suggested fields:

```sql
id BIGSERIAL PRIMARY KEY,
player_id BIGINT NOT NULL REFERENCES players(id),
activity_type TEXT NOT NULL,
source_system TEXT NULL,
source_endpoint TEXT NULL,
source_payload_version TEXT NULL,
account_profile_type TEXT NULL,
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

The nullable item, XP, and KC fields are convenience fields for common single-fact activities.

RuneLite plugin payloads may contain batches, such as one loot event with many item IDs or one boss event with a full KC map. The raw payload should always be preserved, and normalized child rows should be used when the payload contains repeated data.

## activity_event_items

Stores item rows extracted from activity payloads.

This is useful for plugin loot events and collection log snapshots, where a single payload can contain many item IDs.

Suggested fields:

```sql
id BIGSERIAL PRIMARY KEY,
activity_event_id BIGINT NOT NULL REFERENCES activity_events(id),
item_id INT NOT NULL,
item_name TEXT NULL,
quantity INT NOT NULL DEFAULT 1,
source TEXT NULL,
metadata_json JSONB NOT NULL DEFAULT '{}'
```

Recommended indexes:

```sql
CREATE INDEX idx_activity_event_items_item
ON activity_event_items (item_id);

CREATE INDEX idx_activity_event_items_activity
ON activity_event_items (activity_event_id);
```

## activity_event_metrics

Stores key/value metrics extracted from snapshot-like activity payloads.

This is useful for boss KC maps, combat achievement IDs, collection log quantities, and future plugin state snapshots.

Suggested fields:

```sql
id BIGSERIAL PRIMARY KEY,
activity_event_id BIGINT NOT NULL REFERENCES activity_events(id),
metric_type TEXT NOT NULL,
metric_key TEXT NOT NULL,
metric_value BIGINT NULL,
metric_bool BOOLEAN NULL,
metadata_json JSONB NOT NULL DEFAULT '{}'
```

Examples:

```text
metric_type = boss_kc
metric_key = Zulrah
metric_value = 1265

metric_type = combat_achievement
metric_key = 123
metric_bool = true

metric_type = collection_log_item
metric_key = 22486
metric_value = 1
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

## bingo_tile_tiers

A tier is a first-class goal within a tile.

Tiers can have different rule types, and the UI/backend must distinguish a tier being achieved from a tier being scored.

Suggested fields:

```sql
id BIGSERIAL PRIMARY KEY,
tile_id BIGINT NOT NULL REFERENCES bingo_tiles(id),
tier_number INT NOT NULL,
title TEXT NULL,
description TEXT NULL,
score_value INT NOT NULL DEFAULT 1,
is_required_for_board_completion BOOLEAN NOT NULL DEFAULT true,
sort_order INT NOT NULL DEFAULT 0,
config_json JSONB NOT NULL DEFAULT '{}'
```

Recommended constraint:

```sql
UNIQUE (tile_id, tier_number)
```

## tile_rules

Rules define how a tile tier progresses.

A tier may have one or more rules. A single tile may have tiers with different rule types.

Suggested fields:

```sql
id BIGSERIAL PRIMARY KEY,
tile_id BIGINT NOT NULL REFERENCES bingo_tiles(id),
tile_tier_id BIGINT NULL REFERENCES bingo_tile_tiers(id),
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
external_competition_metric
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

This row stores the aggregate tile-level view. Tier-level achieved/scored state should be stored in `event_tile_tier_progress`.

## event_tile_tier_progress

Stores current progress for one tier within an event tile.

Suggested fields:

```sql
id BIGSERIAL PRIMARY KEY,
event_id BIGINT NOT NULL REFERENCES events(id),
tile_id BIGINT NOT NULL REFERENCES bingo_tiles(id),
tile_tier_id BIGINT NOT NULL REFERENCES bingo_tile_tiers(id),
team_id BIGINT NULL REFERENCES event_teams(id),
player_id BIGINT NULL REFERENCES players(id),
current_value NUMERIC NOT NULL DEFAULT 0,
is_achieved BOOLEAN NOT NULL DEFAULT false,
achieved_at TIMESTAMPTZ NULL,
is_scored BOOLEAN NOT NULL DEFAULT false,
scored_at TIMESTAMPTZ NULL,
score_awarded INT NOT NULL DEFAULT 0,
updated_at TIMESTAMPTZ NOT NULL DEFAULT now(),
metadata_json JSONB NOT NULL DEFAULT '{}'
```

Recommended constraint:

```sql
UNIQUE (event_id, tile_tier_id, team_id, player_id)
```

## event_progress_contributions

Auditable record of each activity contribution.

Suggested fields:

```sql
id BIGSERIAL PRIMARY KEY,
event_id BIGINT NOT NULL REFERENCES events(id),
tile_id BIGINT NOT NULL REFERENCES bingo_tiles(id),
tile_tier_id BIGINT NULL REFERENCES bingo_tile_tiers(id),
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

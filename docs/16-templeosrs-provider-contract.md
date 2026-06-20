# 16 - TempleOSRS Provider Contract

This document records the TempleOSRS API contract researched on 2026-06-20.

TempleOSRS is the MVP source of truth for XP/KC competition gains. This app caches Temple responses in PostgreSQL, then rule evaluation and page rendering read only cached database rows.

Source documentation:

```text
https://templeosrs.com/api_doc.php
```

## MVP Base Contract

Provider:

```text
templeosrs
```

Base URL:

```text
https://templeosrs.com/api/
```

The MVP client should support public read endpoints for result sync and scoped write endpoints for pushing finalized participant/team rosters into a linked competition.

TempleOSRS write endpoints require the competition key returned when a competition is created. Treat that key as a secret.

Do not call TempleOSRS from:

```text
rule evaluation
event page rendering
board page rendering
tile details rendering
```

Only explicit Temple integration flows should call TempleOSRS:

```text
Temple result sync worker
public/admin sync request handlers that enqueue sync
admin roster/team export workflow
admin validation workflow
```

## Primary Sync Endpoint

Use Competition Information V2 for metric sync:

```http
GET https://templeosrs.com/api/competition_info_v2.php?id={competitionId}&details=1
```

Supported query parameters relevant to this app:

| Parameter | MVP Use |
|---|---|
| `id` | Required. Temple competition ID from the competition URL. |
| `skill` | Optional. Skill, boss, or metric name in lowercase, or Temple numeric index. Use when a rule wants a non-default metric for the linked competition. |
| `details` | Include with any value. Adds start/end XP/KC values for each participant. MVP should always request it. |
| `snapshot` | Not needed for live scoring. May be useful later for historical/debug views. |
| `format` | Do not use for app sync. JSON default is the canonical MVP format. Spreadsheet formats such as `table`, `tableTeams`, or `csv` are not needed. |
| `altunranked` | Do not use unless the event explicitly wants Temple's alternate unranked boss handling. |

Official docs say participants are ranked by the competition's default skill if no `skill` parameter is provided.

## Competition Information Shape

Observed response shape:

```json
{
  "data": {
    "info": {},
    "participants": [],
    "teams": {}
  }
}
```

Important `data.info` fields:

| Field | Meaning |
|---|---|
| `id` | Temple competition ID. |
| `name` | Competition name. |
| `status` | `0` upcoming, `1` ongoing, `2` finished. |
| `participant_count` | Number of profiles in the competition. |
| `team_competition` | `1` for Temple team competitions, otherwise `0`. |
| `skill_competition` | Temple flag documented as `0 = skill`, `1 = boss`. |
| `skill` | Default skill/boss/metric display name. |
| `skill_index` | Temple metric index. |
| `start_date`, `end_date`, `now_time` | UTC timestamps as strings. |
| `start_date_unix`, `end_date_unix`, `now_time_unix` | UTC timestamps as Unix seconds. |
| `linked_group_id`, `linked_group_name` | Linked Temple group if set. |
| `group_member_sync` | Whether participant list sync to group is enabled. |
| `league_v_competition`, `league_vi_competition` | League competition flags when present. |

Important participant fields:

| Field | Meaning |
|---|---|
| `username` | RuneScape name. Use this as the fallback identity key. |
| `player_name_with_capitalization` | RuneScape name with in-game capitalization/special symbols when present. |
| `gain` | Temple-returned competition gain for the requested/default metric. |
| `start_xp` | Temple start value. For boss competitions this still uses the `xp` field name but represents the relevant Temple value. |
| `end_xp` | Temple current/end value. |
| `last_checked`, `last_checked_unix` | Last Temple check time. |
| `last_changed`, `last_changed_unix` | Last gain-change time. |
| `first_checked_during_comp_unix`, `last_checked_during_comp_unix` | Present in live responses with `details=1`. |
| `team`, `team_name` | Temple team assignment when the Temple competition is team based. |
| `detailed_gains` | Present in live responses. Object keyed by metric name with start/end values for many metrics. |
| `has_datapoints` | Whether Temple has competition datapoints for the participant. |
| `on_hiscores` | Whether the participant was found on hiscores at last update. |

Important team fields:

| Field | Meaning |
|---|---|
| `name` | Temple team name. |
| `start_xp` | Temple team start value. |
| `end_xp` | Temple team current/end value. |
| `gain` | Temple team gain. |
| `mvp` | Temple team MVP name. |
| `members` | Team member names. |

Live responses return `teams` as an object keyed by rank/team number, not necessarily as an array. The client should parse it flexibly.

## Secondary Endpoints

Competition Members:

```http
GET https://templeosrs.com/api/compmembers.php?id={competitionId}
```

This returns an array of RuneScape names. It is useful as a lightweight membership fallback, but it does not include gains, totals, or team values, so it is not enough for scoring.

Group Competitions:

```http
GET https://templeosrs.com/api/group_competitions.php?id={groupId}
```

This returns competitions linked to a Temple group and is useful for future clan competition discovery or event draft creation.

Important fields:

```text
id
name
skill_index
skill
start_date
start_date_unix
end_date
end_date_unix
status
status_text
group_member_sync
participant_count
team_competition
```

## Write/Export Endpoints

TempleOSRS write endpoints are POST endpoints and the official docs say parameters should be included in the request body.

Competition Create:

```http
POST https://templeosrs.com/api/competition_create.php
```

This endpoint creates a competition and returns the competition key. For the current workflow, admins may create the Temple competition manually before linking it in SwedesEventPlanner, so app-side creation is optional for MVP.

Relevant parameters:

| Parameter | Use |
|---|---|
| `name` | Competition name. |
| `skill` | Skill/boss/custom metric name lowercase or Temple numeric index. |
| `team-comp` | Boolean value for team competition. |
| `participants` | Array of RuneScape names for regular competitions. |
| `teams` | JSON object for team competitions. Key is team name, value is comma-separated team members. |
| `start-date` | Start time as Unix timestamp, UTC. |
| `end-date` | End time as Unix timestamp, UTC. |
| `group-id` | Optional linked group ID. |
| `group-key` | Optional linked group key. Treat as secret. |
| `group-sync` | Boolean for syncing participant list with linked group. |

Competition Add Participant:

```http
POST https://templeosrs.com/api/competition_add_participant.php
```

Relevant parameters:

| Parameter | Use |
|---|---|
| `id` | Temple competition ID. |
| `key` | Temple competition key. Secret. |
| `players` | Comma-separated RuneScape names to add. |
| `teams` | JSON object where key is player name and value is team name. Used for team assignments when adding players. |

Competition Remove Participant:

```http
POST https://templeosrs.com/api/competition_remove_participant.php
```

Relevant parameters:

| Parameter | Use |
|---|---|
| `id` | Temple competition ID. |
| `key` | Temple competition key. Secret. |
| `players` | Comma-separated RuneScape names to remove. |

## Write/Export Caveats

The official docs do not currently document a full "replace competition roster/team assignment" endpoint.

Before implementing destructive reconciliation, verify against a test Temple competition:

```text
whether adding an already-present participant is ignored or updates fields
whether competition_add_participant.php updates team assignment for existing participants
whether removing and re-adding a participant preserves expected competition state
whether removal is allowed/appropriate after a competition has started
```

For MVP, implement export as an admin-triggered, logged workflow with preview/dry-run output.

Export should avoid surprises:

```text
show intended participants and teams before pushing
add missing participants
push team mapping for team competitions
only remove participants when the admin explicitly confirms removal
validate Temple state after export
log all attempts and safe errors
```

## Cache Mapping

Map Temple competition metadata to `external_competitions`:

```text
provider = templeosrs
external_id = data.info.id
name = data.info.name
metric_type = xp or kc, from app configuration/rule config
metric_key = data.info.skill or rule-selected metric
competition_mode = individual or team, from Temple info/team_competition and local config
secret_reference = environment/deployment secret reference, not the Temple key itself
last_successful_sync_at = sync completion time after successful cache write
provider_config_json = safe provider metadata
```

Map participant rows to `external_competition_metrics`:

```text
external_competition_id
runescape_name = username
display name in metadata = player_name_with_capitalization, when present
player_id = matched local player, otherwise null
metric_type
metric_key
start_value = start_xp
current_value = end_xp
gained_value = gain
rank = participant order/rank when available
last_synced_at = sync completion time
metadata_json = Temple timestamps, team/team_name, has_datapoints, on_hiscores, detailed_gains subset or raw-safe details
```

Store unresolved Temple identities separately enough that admins can:

```text
Create player
Link to existing player
Ignore for this external competition
```

Unmatched rows with `player_id = null` must not contribute to event progress.

Map Temple team rows to `external_competition_team_metrics`:

```text
external_competition_id
local_team_id = matched SwedesEventPlanner team, when known
temple_team_key = Temple team number/key
team_name = Temple team name
metric_type
metric_key
start_value = start_xp
current_value = end_xp
gained_value = gain
rank = team order/rank when available
mvp_runescape_name = mvp
members_json = Temple member names
last_synced_at = sync completion time
metadata_json = safe Temple team metadata
```

## Team Scoring Rules

Temple returns both per-player gains and Temple team totals.

For MVP XP/KC team tiles:

```text
if linked Temple competition is team-based:
  use cached Temple-returned team totals as the primary scoring input

if linked Temple competition is not team-based:
  use cached Temple-returned per-player gains grouped by local SwedesEventPlanner event teams
```

Always cache per-player gains for audit/debugging.

Always cache Temple team members/totals when Temple returns them.

Surface mismatches between Temple teams and local SwedesEventPlanner teams in admin/testing views.

SwedesEventPlanner remains the source of truth for finalized roster/team export to TempleOSRS. TempleOSRS remains the source of truth for the XP/KC values returned after sync.

## Numeric Values

MVP XP/KC tiles use integer values.

Temple also exposes EHP/EHB/custom-style metrics in some competition/group discovery data, and live `detailed_gains` can include decimal-like values. If the app caches anything beyond strict XP/KC, use `NUMERIC` for cached values and rule progress. If MVP schema uses `BIGINT` for `external_competition_metrics`, keep that intentionally scoped to XP/KC.

## Provider Behavior Notes

TempleOSRS Competition Information V2 is the non-deprecated competition info endpoint. The older Competition Information endpoint is documented as deprecated and should not be used for MVP.

The TempleOSRS changelog says that as of 2025-12-05, Competition Information V2 returns unranked skills/bosses as `0` by default. That value should be treated as Temple source-of-truth data. Do not repair it locally.

The `altunranked` parameter changes unranked boss behavior. Do not enable it by default because it changes scoring semantics.

No public GET authentication requirement was observed for the read endpoints above. No official rate limit was found in the API documentation during this pass, so the application should remain conservative with periodic syncs, public cooldowns, and duplicate-job prevention.

Temple write endpoints require a competition key. Store the key as a secret outside committed config. Do not log it, return it through APIs, or store it in raw request/response metadata.

## Implementation Guardrails

The Temple client should:

```text
use HttpClientFactory
set a clear User-Agent
use configurable base URL and timeout
deserialize defensively
store safe raw metadata for diagnostics where useful
avoid logging secrets or full sensitive payloads
record failed attempts in external_competition_sync_runs
record roster export attempts in external_competition_export_runs
avoid concurrent duplicate sync jobs per external competition
```

Rule evaluation should never read raw Temple response blobs. It should read normalized cached metric rows.

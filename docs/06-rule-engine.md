# 06 - Rule Engine

## Purpose

The rule engine determines whether an activity event contributes to an event goal or bingo tile.

The code should define how rule types work. The database should define the configuration for each rule.

```text
Rule type is code.
Rule config is data.
```

## Rule input

A rule evaluation should receive:

```text
activity event
participant/event context
rule configuration
current progress state
optional snapshot/baseline data
```

## Rule output

A rule evaluation should return:

```text
matched: true/false
valueAdded: number
description: text
metadata: json
```

Example:

```json
{
  "matched": true,
  "valueAdded": 7,
  "description": "Scythe of vitur from Theatre of Blood, +7 points",
  "metadata": {
    "itemId": 22486,
    "points": 7
  }
}
```

## Initial rule types

Recommended MVP rule types:

```text
item_count
point_threshold
external_competition_metric
manual
```

Future rule types:

```text
xp_gained
kc_gained
collection_log_obtained
claimed_reward
composite
```

## Common rule fields

Many rules should support:

```text
activityType
source
itemIds
itemGroupKey
duplicatesCount
tiers
scope
```

## Scope

Rule scope determines whose progress is updated.

Recommended scope values:

```text
player
team
event
```

For team bingo, most rules should use `team` scope.

## Tiers

Many goals have tiers, but tiers are first-class goals rather than only thresholds inside one rule.

A tier has its own rule or rules. Different tiers on the same tile may use different rule types.

Example:

```json
{
  "tileTierId": 1001,
  "ruleType": "point_threshold",
  "config": {
    "required": 10
  }
}
```

Tier achievement is derived from the tier's current value and rule result.

Example:

```text
current value = 18
required value = 10
tier achieved = true
```

Tier scoring may be separate from achievement. For cumulative category/tier layouts, a later tier can be achieved before earlier tiers are scored, but should not award score until prerequisite lower tiers have scored.

Tier progress rows are the source of truth for visible progress. A tile-level aggregate can exist for compatibility and sorting, but public UI should display each tier's own current value, target, achieved state, and scored state. Tile-level progress should not mix unrelated units from different tiers.

When a rule contributes to one tier, all other tiers on the same tile with the same rule type and same metric configuration, ignoring only threshold fields such as `required`, `requiredValue`, or `tiers`, should receive the same cumulative progress value. This lets point-threshold tiers share one points table while keeping each tier's target independent.

Scoring is recalculated in tier order. If a later tier was already achieved, it should become scored immediately after all earlier required tiers become scored.

## item_count rule

Counts matching item drops.

Example config:

```json
{
  "activityType": "item_drop",
  "itemGroupKey": "soulreaper_axe_pieces",
  "duplicatesCount": true,
  "tiers": [
    { "tier": 3, "required": 4 }
  ]
}
```

Behavior:

```text
If activity is item_drop and item is in the group, add quantity.
```

If `duplicatesCount` is false, the rule should count distinct item IDs only. This requires checking previous contributions or metadata.

## point_threshold rule

Awards points based on matching activity.

Example TOB config:

```json
{
  "activityType": "item_drop",
  "source": ["Theatre of Blood"],
  "pointsTable": [
    { "itemId": 22477, "name": "Avernic defender hilt", "points": 1 },
    { "itemId": 22324, "name": "Ghrazi rapier", "points": 3 },
    { "itemId": 22481, "name": "Sanguinesti staff", "points": 3 },
    { "itemId": 22486, "name": "Scythe of vitur", "points": 7 }
  ],
  "tiers": [
    { "tier": 1, "required": 10 },
    { "tier": 2, "required": 25 },
    { "tier": 3, "required": 45 }
  ]
}
```

Behavior:

```text
If activity item matches an entry in pointsTable, add that many points.
```

Targets are cumulative totals, not additional remaining values. For example, if a Scythe is worth 7 points, two Scythes give 14 total points. With TOB targets of 10, 25, and 50, tier 1 is scored and tier 2 displays 14 / 25 progress.

## xp_gained rule

Future/plugin-snapshot rule type.

Calculates XP gained since a baseline.

Example config:

```json
{
  "activityType": "xp_snapshot",
  "skill": "Slayer",
  "baseline": "event_start",
  "tiers": [
    { "tier": 1, "required": 5000000 },
    { "tier": 2, "required": 15000000 }
  ]
}
```

XP rules should not simply add each snapshot's XP. They should compare the current snapshot against an event-specific baseline.

Example:

```text
Baseline Slayer XP: 42,000,000
Current Slayer XP: 47,300,000
Progress: 5,300,000
```

The progress update should set the current value to the calculated XP gained, not add the full XP amount repeatedly.

## kc_gained rule

Future/plugin-snapshot rule type.

Similar to XP gained, but for boss kill count.

Example config:

```json
{
  "activityType": "kc_snapshot",
  "bossName": "Zulrah",
  "baseline": "event_start",
  "tiers": [
    { "tier": 1, "required": 100 }
  ]
}
```

Behavior:

```text
Progress = current KC - baseline KC
```

## external_competition_metric rule

MVP rule type for XP/KC tiles backed by cached TempleOSRS competition data.

This rule must not call TempleOSRS directly.

Rule evaluation reads `external_competition_metrics` rows only. Those rows are refreshed by the Temple sync worker.

If cached data is missing, stale, or from a failed sync, the rule should still evaluate from the cached rows that exist. It should not fetch live TempleOSRS data as a fallback.

Example XP config:

```json
{
  "provider": "templeosrs",
  "externalCompetitionId": 123,
  "metricType": "xp",
  "metricKey": "Slayer",
  "required": 5000000,
  "valueField": "gained_value"
}
```

Example KC config:

```json
{
  "provider": "templeosrs",
  "externalCompetitionId": 456,
  "metricType": "kc",
  "metricKey": "Zulrah",
  "required": 100,
  "valueField": "gained_value"
}
```

For team-scoped rules:

```text
team-based linked Temple competition:
  team value = Temple-returned cached team gained value/total

non-team linked Temple competition:
  team value = sum(gained_value for known players on that local event team)
```

For team-based Temple competitions, still cache per-player gains for audit/debugging and surface Temple/local team mismatches in admin/testing views.

For player-scoped rules:

```text
player value = gained_value for that participant
```

TempleOSRS competition gain remains the scoring source. Plugin XP/KC snapshots should not be used for MVP scoring.

Because this rule reads cached aggregate state rather than a single incoming activity event, progress should be recalculated after external competition syncs and contribution records should describe the sync/delta that caused the visible progress change.

For tier-backed rules, the sync delta should be calculated from that tier's previous current value, not from `event_tile_progress.current_value`. This prevents an XP/KC tier from overwriting or confusing item-count or point-threshold tiers on the same tile.

Progress may increase or decrease if TempleOSRS returns changed gains. The rule should not maintain a separate monotonic scoring value or infer/repair Temple values locally.

## manual rule

Manual rules allow admin-controlled progress.

Example use cases:

```text
Unknown tracking method
Tile not supported yet
Disputed progress
Special event condition
```

Manual progress should still create contributions so it is auditable.

## collection_log_obtained rule

Future rule type.

Tracks collection log entries obtained during an event.

Potential config:

```json
{
  "activityType": "collection_log_entry",
  "itemGroupKey": "jars",
  "tiers": [
    { "tier": 1, "required": 2 },
    { "tier": 2, "required": 4 },
    { "tier": 3, "required": 7 }
  ]
}
```

## claimed_reward rule

Future rule type.

Useful for rewards that should count only when claimed from a chest or reward interface.

Example:

```json
{
  "activityType": "claimed_reward",
  "source": ["Fortis Colosseum reward chest"],
  "itemGroupKey": "ralos",
  "tiers": [
    { "tier": 3, "required": 1 }
  ]
}
```

## Unlock conditions

Unlock conditions should be checked before applying progress.

Example:

```json
{
  "conditionType": "tile_tier_completed",
  "config": {
    "requiredTileId": 123,
    "requiredTier": 2
  }
}
```

For MVP, tiles can default to unlocked. The table can still exist so the database is ready for advanced events.

## Composite conditions

Future advanced conditions can support:

```text
all
any
not
```

Example:

```json
{
  "type": "all",
  "conditions": [
    { "type": "tile_completed", "tileId": 1 },
    {
      "type": "any",
      "conditions": [
        { "type": "tile_completed", "tileId": 2 },
        { "type": "points_reached", "points": 50 }
      ]
    }
  ]
}
```

## Rule evaluation should be deterministic

Given the same activity, event context, and rule config, the rule engine should produce the same result.

This helps with:

```text
rebuilds
bug fixes
audits
testing
```

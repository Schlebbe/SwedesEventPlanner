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
xp_gained
kc_gained
manual
```

Future rule types:

```text
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

Many goals have three tiers.

Example:

```json
{
  "tiers": [
    { "tier": 1, "required": 10 },
    { "tier": 2, "required": 25 },
    { "tier": 3, "required": 45 }
  ]
}
```

Current tier is derived from current value.

Example:

```text
current value = 18
required tier 1 = 10
required tier 2 = 25
current tier = 1
```

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

## xp_gained rule

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


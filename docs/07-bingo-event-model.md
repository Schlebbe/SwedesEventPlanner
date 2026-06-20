# 07 - Bingo Event Model

## Purpose

Bingo is the first event format. It should be implemented as one event type on top of the generic event and rule system.

Bingo-specific concepts:

```text
board
tile
tier
team progress
tile contributions
optional unlock conditions
```

## Current board structure

The current planning format is a category-based board with three tiers per category.

Example:

| Content | Tier 1 | Tier 2 | Tier 3 |
|---|---:|---:|---:|
| TOB | 10 pts | 25 pts | 45 pts |
| COX | 10 pts | 25 pts | 45 pts |
| TOA | 10 pts | 25 pts | 45 pts |
| Pets | 1 pet | 3 pets | 6 pets |
| Slayer | 5m XP | 15m XP | Heart / Eternal Gem |

This can still be represented as bingo tiles, even if the UI is not a traditional 5x5 board.

## Board

A board belongs to one event.

Example:

```json
{
  "eventId": 55,
  "name": "Summer Clan Bingo 2026",
  "rows": null,
  "columns": null,
  "config": {
    "layout": "category_tiers",
    "tiersPerTile": 3
  }
}
```

## Tile

A tile represents one content category or goal group.

Example tile:

```json
{
  "title": "TOB",
  "description": "Earn points from Theatre of Blood uniques",
  "sortOrder": 1
}
```

The tile can have one or more tiers.

## Tiers

Tiers should be modeled as first-class goals.

A tier can have its own rules, and tiers on the same tile may use different rule types.

Example:

```json
{
  "tileId": 10,
  "tierNumber": 1,
  "title": "TOB Tier 1",
  "scoreValue": 1,
  "isRequiredForBoardCompletion": true
}
```

A tile can be partially complete if tier 1 or tier 2 has scored but tier 3 has not.

The system should distinguish:

```text
achieved = the condition happened
scored = the tier counts toward board completion and points
```

For cumulative tier layouts, a later tier may be achieved before earlier tiers are scored. In that case, the later tier should not award score until the earlier required tiers are also scored.

Example:

```text
Tier 3 objective: receive a Scythe of vitur
Team receives a Scythe while tier 1 and tier 2 are still incomplete
Tier 3 is achieved
Tier 3 is not scored
Tier 3 scores only after tier 1 and tier 2 are scored
```

## Grid completion modes

Grid boards should support configurable completion modes.

Planned modes:

```text
full_board
any_line
specific_number_of_lines
all_lines
custom
```

MVP should support at least:

```text
full_board
any_line
```

The primary expected mode is `full_board`, but the board completion logic should not hardcode one meaning for all grid boards.

## Team-based progress

For clan bingo, progress should usually be team-scoped.

```text
Team A gets a Scythe
  -> Team A TOB tile +7
```

The contribution should still store the individual player who caused the progress.

```text
event_progress_contributions.player_id = player who got the drop
event_progress_contributions.team_id = their team in that event
```

## Example TOB tile

Tile:

```json
{
  "title": "TOB",
  "description": "Earn points from Theatre of Blood uniques"
}
```

Rule:

```json
{
  "ruleType": "point_threshold",
  "tileTierId": 1001,
  "scope": "team",
  "config": {
    "activityType": "item_drop",
    "source": ["Theatre of Blood"],
    "pointsTable": [
      { "itemId": 22477, "name": "Avernic defender hilt", "points": 1 },
      { "itemId": 22324, "name": "Ghrazi rapier", "points": 3 },
      { "itemId": 22481, "name": "Sanguinesti staff", "points": 3 },
      { "itemId": 22486, "name": "Scythe of vitur", "points": 7 }
    ],
    "required": 10
  }
}
```

Additional TOB tiers would have their own `bingo_tile_tiers` rows and rules with different `required` values.

## Example Yama tile with cumulative armor

Requirement:

```text
Tier 2 requires 1 armor.
Tier 3 requires 3 armor total.
```

Rule:

```json
{
  "ruleType": "item_count",
  "scope": "team",
  "config": {
    "activityType": "item_drop",
    "source": ["Yama"],
    "itemGroupKey": "yama_armor",
    "duplicatesCount": true,
    "tiers": [
      { "tier": 2, "required": 1 },
      { "tier": 3, "required": 3 }
    ]
  }
}
```

This means 1 armor completes tier 2, and 3 total armor completes tier 3.

## Example Soulreaper axe tile

Requirement:

```text
Any 4 Soulreaper axe pieces. Duplicates count.
```

Rule:

```json
{
  "ruleType": "item_count",
  "scope": "team",
  "config": {
    "activityType": "item_drop",
    "itemGroupKey": "soulreaper_axe_pieces",
    "duplicatesCount": true,
    "tiers": [
      { "tier": 3, "required": 4 }
    ]
  }
}
```

## Example Slayer XP tile

Requirement:

```text
Tier 1: 5m Slayer XP gained
Tier 2: 15m Slayer XP gained
```

Rule:

```json
{
  "ruleType": "external_competition_metric",
  "tileTierId": 2001,
  "scope": "team",
  "config": {
    "provider": "templeosrs",
    "externalCompetitionId": 123,
    "metricType": "xp",
    "metricKey": "Slayer",
    "required": 5000000,
    "valueField": "gained_value"
  }
}
```

For team scope, each player's cached TempleOSRS gained XP is summed for the team.

Additional Slayer tiers would have their own `bingo_tile_tiers` rows and rules with different `required` values.

## Example Pets tile

Requirement:

```text
Tier 1: 1 pet
Tier 2: 3 pets
Tier 3: 6 pets
```

Rule:

```json
{
  "ruleType": "item_count",
  "scope": "team",
  "config": {
    "activityType": "item_drop",
    "itemGroupKey": "pets",
    "duplicatesCount": true,
    "tiers": [
      { "tier": 1, "required": 1 },
      { "tier": 2, "required": 3 },
      { "tier": 3, "required": 6 }
    ]
  }
}
```

## Unlocking tiles

Tiles may optionally require another tile or tier before they can progress.

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

For MVP, all tiles can be unlocked by default. The table and model should exist for future use.

## Bingo scoring

Bingo scoring can be separate from tile progress.

Possible scoring models:

```text
score by completed tiers
score by completed tiles
score by full rows/columns/diagonals
score by total progress points
score by custom event rules
```

The MVP can display tile progress without needing to finalize every possible scoring system.

## Contribution history

Each tile should show why it has progress.

Example:

```text
TOB: 18 / 25 points

Contributions:
- Player A: Scythe of vitur, +7
- Player B: Avernic defender hilt, +1
- Player C: Ghrazi rapier, +3
```

This should come from `event_progress_contributions`.

## Board completion

For category/tier boards, a board is complete when all required tiers are scored, not merely achieved.

For grid boards, board completion is determined by the board's configured completion mode.

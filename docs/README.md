# Clan Event Platform Documentation

This documentation describes a modular clan event platform that ingests player activity, stores it globally, and evaluates that activity against one or more active events.

The intended first implementation is a backend, database, and website using mocked player activity. A RuneLite plugin can be added later as another activity source.

## Document map

| Document | Purpose |
|---|---|
| [01 - Product Overview](01-product-overview.md) | High-level goals, principles, and MVP shape |
| [02 - System Architecture](02-system-architecture.md) | Application boundaries and how data flows through the system |
| [03 - Database Model](03-database-model.md) | Core tables and relationships |
| [04 - Activity Ingestion](04-activity-ingestion.md) | How mocked/plugin activity is received and stored |
| [05 - Event Processing](05-event-processing.md) | Queue/worker model for evaluating activity against active events |
| [06 - Rule Engine](06-rule-engine.md) | Configurable tile/event rules and rule types |
| [07 - Bingo Event Model](07-bingo-event-model.md) | How bingo boards, tiles, tiers, teams, and progress should work |
| [08 - XP and Snapshot Handling](08-xp-and-snapshot-handling.md) | Baselines, desync prevention, login/logout snapshots, and event-start safeguards |
| [09 - Mock Activity and Simulation](09-mock-activity-and-simulation.md) | How to test without a real RuneLite plugin |
| [10 - MVP Scope](10-mvp-scope.md) | What belongs in the first version and what should be deferred |
| [11 - Glossary](11-glossary.md) | Shared terminology |

## Core principle

```text
Activity is global.
Event progress is event-specific.
Rules are configurable.
The activity source does not need to know about active events.
```

A player activity event, such as receiving a raid unique, should be stored once. The backend should then evaluate that activity against every active event where the player is participating.


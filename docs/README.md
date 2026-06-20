# Clan Event Platform Documentation

This documentation describes a modular clan event platform that ingests player activity, stores it globally, syncs external competition metrics where needed, and evaluates cached facts against one or more active events.

The intended first implementation is a backend, database, and website using mocked player activity plus cached TempleOSRS competition data for XP/KC tiles. A RuneLite plugin can be added later as another activity source.

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
| [08 - XP/KC Source and Snapshot Handling](08-xp-and-snapshot-handling.md) | Temple-backed XP/KC scoring plus future plugin snapshot baseline handling |
| [09 - Mock Activity and Simulation](09-mock-activity-and-simulation.md) | How to test mocked activity and cached external metrics without a real RuneLite plugin |
| [10 - MVP Scope](10-mvp-scope.md) | What belongs in the first version and what should be deferred |
| [11 - Glossary](11-glossary.md) | Shared terminology |
| [12 - Implementation Decisions](12-implementation-decisions.md) | Confirmed stack, deployment target, MVP UI, and scoring decisions |
| [13 - RuneLite Plugin Reference](13-runelite-plugin-reference.md) | Useful payload shapes and gaps from the Valiance plugin without preserving legacy endpoints |
| [14 - Raspberry Pi Deployment Notes](14-raspberry-pi-deployment-notes.md) | Existing Pi environment, reusable deployment patterns, and constraints |
| [15 - External Competition Sync](15-external-competition-sync.md) | TempleOSRS competition sync, cached metrics, cooldowns, and XP/KC scoring source |
| [16 - TempleOSRS Provider Contract](16-templeosrs-provider-contract.md) | Exact TempleOSRS endpoints, response shapes, cache mapping, and provider caveats |
| [17 - Event Setup Workflow](17-event-setup-workflow.md) | Admin workflow for signup import, team assignment, Temple export, validation, and scoring |
| [18 - Local Development Database](18-local-dev-database.md) | Windows local PostgreSQL setup and secret handling notes |

## Core principle

```text
Activity is global.
Event progress is event-specific.
Rules are configurable.
The activity source does not need to know about active events.
External providers sync into cached database rows.
Rules and UI do not call external providers directly.
```

A player activity event, such as receiving a raid unique, should be stored once. The backend should then evaluate that activity against every active event where the player is participating.

# 05 - Event Processing

## Purpose

Event processing evaluates newly received activity against all relevant active events.

Events should not constantly poll the database for new activity. Instead, activity ingestion creates a processing job, and a worker processes those jobs.

External competition processing is separate from raw activity ingestion. TempleOSRS sync jobs refresh cached competition metrics and then recalculate affected `external_competition_metric` rules.

## Processing flow

```text
POST /api/activity
  -> insert activity_events
  -> insert activity_processing_queue
  -> return success

ActivityWorker
  -> load queued activity
  -> find active participations for that player
  -> evaluate matching rules
  -> insert progress contributions
  -> update current progress
  -> mark job processed

TempleSyncWorker
  -> refresh linked TempleOSRS competition
  -> update cached external_competition_metrics
  -> record external_competition_sync_runs
  -> recalculate affected external_competition_metric rules
  -> update current progress
```

`POST /api/activity` is the mock/dev ingestion endpoint used by simulator and local testing tools. Future production RuneLite plugin ingestion should use the canonical `/api/plugin/...` endpoints.

Temple sync jobs should use `external_competition_sync_runs` as their queue/state table, separate from `activity_processing_queue`.

## Why use a queue?

A queue keeps the API fast and makes processing retryable.

Without a queue:

```text
API receives activity
  -> performs all rule checks immediately
  -> request may become slow or fail halfway
```

With a queue:

```text
API receives activity
  -> stores activity
  -> queues processing
  -> returns quickly

Worker processes separately
  -> can retry if processing fails
```

For MVP, a database-backed queue is enough.

## Worker logic

Pseudo-code:

```csharp
public async Task ProcessActivityJob(long activityEventId)
{
    var activity = await activityRepository.Get(activityEventId);

    var participations = await eventRepository
        .GetActiveParticipationsForPlayer(activity.PlayerId, activity.OccurredAt);

    foreach (var participation in participations)
    {
        var rules = await ruleRepository.GetActiveRulesForEvent(participation.EventId);

        foreach (var rule in rules)
        {
            var result = await ruleEngine.Evaluate(activity, participation, rule);

            if (!result.Matched)
                continue;

            await progressService.ApplyContribution(new ProgressContribution
            {
                EventId = participation.EventId,
                TileId = rule.TileId,
                RuleId = rule.Id,
                TeamId = participation.TeamId,
                PlayerId = activity.PlayerId,
                ActivityEventId = activity.Id,
                ValueAdded = result.ValueAdded,
                Description = result.Description,
                Metadata = result.Metadata
            });
        }
    }
}
```

## Finding active participations

The worker should not ask:

```text
What is the current active event?
```

It should ask:

```text
Which active events is this player participating in at this activity time?
```

Recommended filters:

```text
event_participants.player_id = activity.player_id
event_participants.status = 'active'
events.status = 'active'
activity.occurred_at >= events.starts_at
activity.occurred_at <= events.ends_at, when ends_at is not null
```

For MVP, event status is manually controlled, but the event time window must still be enforced. Future work can add automatic status transitions.

For team-scoped events, active participants with no team assignment should not contribute progress until assigned to a team. Surface those players in admin/testing views.

## Rule matching

Do not load and evaluate every rule in the system.

At minimum, filter by event. Ideally also pre-filter by activity type.

Example:

```text
Activity type: item_drop
Only evaluate rules with activityType item_drop or generic/manual rules.
```

For Temple-backed XP/KC tiles:

```text
Sync source: external competition metrics
Only recalculate rules linked to the synced external competition.
Do not call TempleOSRS from the rule engine.
```

Rule recalculation must read cached `external_competition_metrics` only.

## Applying progress

When a rule matches activity, insert a contribution before updating current progress.

```text
Insert event_progress_contributions
Update event_tile_progress
```

The contribution table provides auditability and makes progress rebuildable.

For `external_competition_metric` rules, progress should be recalculated from cached competition rows after a sync. Contributions should record the visible delta or audit entry caused by the sync run.

Temple-backed progress can increase or decrease when cached TempleOSRS gains change. Do not maintain a separate monotonic scoring value for XP/KC tiles.

Store Temple recalculation audit/contribution entries per affected tile/team per sync run, including decreases. Negative adjustments should be visible in tile details and admin/testing views, but not spammed into the main participant contribution feed.

UI freshness should use the external competition's last successful sync time, not merely the most recent attempted sync.

## Idempotency

Progress application must be idempotent.

The same activity should not count twice for the same event/tile/rule.

Recommended unique constraint:

```sql
UNIQUE (event_id, tile_id, rule_id, activity_event_id)
```

If a duplicate contribution is attempted, skip it or treat it as already processed.

## Processing failures

If a job fails:

```text
increment attempts
store error_message
set available_at for retry
```

Example retry behavior:

```text
Attempt 1: retry after 10 seconds
Attempt 2: retry after 1 minute
Attempt 3: retry after 5 minutes
After max attempts: mark failed
```

## Worker locking

For a database-backed queue, workers should lock jobs safely.

Conceptual query:

```sql
SELECT *
FROM activity_processing_queue
WHERE status = 'pending'
  AND available_at <= now()
ORDER BY id
FOR UPDATE SKIP LOCKED
LIMIT 10;
```

This allows multiple workers later without processing the same job twice.

Temple sync jobs need the same kind of guard per external competition. An admin force-sync can bypass public cooldown, but if a sync is already queued or running for that competition, the system should not enqueue a duplicate concurrent worker job.

Failed Temple sync jobs should be marked failed in `external_competition_sync_runs` with a useful error message and surfaced in admin/testing views.

## Processing outside event time

Activity outside an event's start/end window should not progress that event.

Important:

```text
Use activity.occurred_at for event eligibility, not received_at.
```

`received_at` is useful for diagnostics and detecting delayed submission.

## Rebuilding progress

Because all activity and contributions are stored, progress should be rebuildable.

A future rebuild process could:

```text
1. Clear progress/contributions for an event.
2. Replay activity_events for participating players within the event window.
3. Re-evaluate rules.
4. Recreate contributions and progress.
```

This is useful if rule configs are corrected after an event starts.

For Temple-backed XP/KC rules, rebuilds should use cached `external_competition_metrics` rows. If fresh Temple data is needed, run a sync first, then rebuild from the cache.

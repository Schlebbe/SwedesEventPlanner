using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using SwedesEventPlanner.Application.Activity;
using SwedesEventPlanner.Application.Clock;
using SwedesEventPlanner.Domain.Activity;
using SwedesEventPlanner.Domain.Bingo;
using SwedesEventPlanner.Domain.Events;
using SwedesEventPlanner.Infrastructure.Bingo;
using SwedesEventPlanner.Infrastructure.Persistence;

namespace SwedesEventPlanner.Infrastructure.Activity;

public sealed class ActivityProcessingService(
    EventPlannerDbContext dbContext,
    IClock clock,
    ILogger<ActivityProcessingService> logger) : IActivityProcessingService
{
    private const int MaxAttempts = 3;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<int> ProcessPendingActivityAsync(
        int maxBatchSize,
        CancellationToken cancellationToken)
    {
        if (maxBatchSize <= 0)
        {
            return 0;
        }

        var jobs = await ClaimPendingJobsAsync(maxBatchSize, cancellationToken);

        foreach (var job in jobs)
        {
            await ProcessClaimedJobAsync(job.Id, job.ActivityEventId, cancellationToken);
        }

        return jobs.Count;
    }

    public async Task ProcessActivityAsync(
        long activityEventId,
        CancellationToken cancellationToken)
    {
        await using var transaction = await BeginTransactionIfRelationalAsync(cancellationToken);

        var activity = await dbContext.ActivityEvents
            .SingleOrDefaultAsync(candidate => candidate.Id == activityEventId, cancellationToken);

        if (activity is null)
        {
            return;
        }

        var activityItems = await dbContext.ActivityEventItems
            .Where(item => item.ActivityEventId == activity.Id)
            .ToListAsync(cancellationToken);

        var participations = await (
                from participant in dbContext.EventParticipants
                join eventDefinition in dbContext.Events on participant.EventId equals eventDefinition.Id
                where participant.PlayerId == activity.PlayerId
                    && participant.Status == EventParticipantStatuses.Active
                    && eventDefinition.Status == EventStatuses.Active
                    && activity.OccurredAt >= eventDefinition.StartsAt
                    && (eventDefinition.EndsAt == null || activity.OccurredAt <= eventDefinition.EndsAt)
                select new ParticipationContext(
                    participant.EventId,
                    participant.PlayerId,
                    participant.TeamId))
            .ToListAsync(cancellationToken);

        foreach (var participation in participations)
        {
            var boardIds = await dbContext.BingoBoards
                .Where(board => board.EventId == participation.EventId)
                .Select(board => board.Id)
                .ToListAsync(cancellationToken);

            var tileIds = await dbContext.BingoTiles
                .Where(tile => boardIds.Contains(tile.BoardId))
                .Select(tile => tile.Id)
                .ToListAsync(cancellationToken);

            var rules = await dbContext.TileRules
                .Where(rule => tileIds.Contains(rule.TileId) &&
                    rule.IsActive &&
                    (rule.RuleType == RuleTypes.ItemCount || rule.RuleType == RuleTypes.PointThreshold))
                .ToListAsync(cancellationToken);

            foreach (var rule in rules)
            {
                if (rule.Scope == RuleScopes.Team && participation.TeamId is null)
                {
                    continue;
                }

                var evaluation = await EvaluateRuleAsync(
                    activity,
                    activityItems,
                    participation,
                    rule,
                    cancellationToken);

                if (!evaluation.Matched)
                {
                    continue;
                }

                await ApplyContributionAsync(
                    activity,
                    participation,
                    rule,
                    evaluation,
                    cancellationToken);
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        if (transaction is not null)
        {
            await transaction.CommitAsync(cancellationToken);
        }
    }

    private async Task<List<ActivityProcessingQueueItem>> ClaimPendingJobsAsync(
        int maxBatchSize,
        CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        await using var transaction = await BeginTransactionIfRelationalAsync(cancellationToken);

        var jobs = dbContext.Database.IsRelational()
            ? await dbContext.ActivityProcessingQueue
                .FromSqlInterpolated($"""
                    SELECT *
                    FROM public.activity_processing_queue
                    WHERE status = {ActivityProcessingStatuses.Pending}
                      AND available_at <= {now}
                    ORDER BY id
                    FOR UPDATE SKIP LOCKED
                    LIMIT {maxBatchSize}
                    """)
                .ToListAsync(cancellationToken)
            : await dbContext.ActivityProcessingQueue
                .Where(job => job.Status == ActivityProcessingStatuses.Pending && job.AvailableAt <= now)
                .OrderBy(job => job.Id)
                .Take(maxBatchSize)
                .ToListAsync(cancellationToken);

        foreach (var job in jobs)
        {
            job.Status = ActivityProcessingStatuses.Processing;
            job.LockedAt = now;
            job.Attempts += 1;
            job.ErrorMessage = null;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        if (transaction is not null)
        {
            await transaction.CommitAsync(cancellationToken);
        }

        return jobs;
    }

    private async Task ProcessClaimedJobAsync(
        long queueItemId,
        long activityEventId,
        CancellationToken cancellationToken)
    {
        try
        {
            await ProcessActivityAsync(activityEventId, cancellationToken);

            var processedAt = clock.UtcNow;
            var queueItem = await dbContext.ActivityProcessingQueue
                .SingleAsync(job => job.Id == queueItemId, cancellationToken);

            queueItem.Status = ActivityProcessingStatuses.Processed;
            queueItem.ProcessedAt = processedAt;
            queueItem.LockedAt = null;
            queueItem.ErrorMessage = null;

            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(
                exception,
                "Failed to process activity queue item {QueueItemId} for activity {ActivityEventId}.",
                queueItemId,
                activityEventId);

            var queueItem = await dbContext.ActivityProcessingQueue
                .SingleAsync(job => job.Id == queueItemId, cancellationToken);

            queueItem.LockedAt = null;
            queueItem.ErrorMessage = exception.Message;

            if (queueItem.Attempts >= MaxAttempts)
            {
                queueItem.Status = ActivityProcessingStatuses.Failed;
            }
            else
            {
                queueItem.Status = ActivityProcessingStatuses.Pending;
                queueItem.AvailableAt = clock.UtcNow.Add(GetRetryDelay(queueItem.Attempts));
            }

            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task<RuleEvaluationResult> EvaluateRuleAsync(
        ActivityEvent activity,
        IReadOnlyCollection<ActivityEventItem> activityItems,
        ParticipationContext participation,
        TileRule rule,
        CancellationToken cancellationToken)
    {
        using var config = ParseRuleConfig(rule.ConfigJson);

        if (!MatchesActivityType(activity, config.RootElement))
        {
            return RuleEvaluationResult.NoMatch;
        }

        if (!MatchesSource(activity, config.RootElement))
        {
            return RuleEvaluationResult.NoMatch;
        }

        return rule.RuleType switch
        {
            RuleTypes.ItemCount => await EvaluateItemCountAsync(
                activityItems,
                participation,
                rule,
                config.RootElement,
                cancellationToken),
            RuleTypes.PointThreshold => EvaluatePointThreshold(activityItems, config.RootElement),
            _ => RuleEvaluationResult.NoMatch
        };
    }

    private async Task<RuleEvaluationResult> EvaluateItemCountAsync(
        IReadOnlyCollection<ActivityEventItem> activityItems,
        ParticipationContext participation,
        TileRule rule,
        JsonElement config,
        CancellationToken cancellationToken)
    {
        var matchingItemIds = await GetConfiguredItemIdsAsync(config, cancellationToken);
        var duplicatesCount = !config.TryGetProperty("duplicatesCount", out var duplicatesElement) ||
            duplicatesElement.ValueKind != JsonValueKind.False;

        var valueAdded = 0m;
        var matchedItems = new List<object>();

        foreach (var item in activityItems)
        {
            if (matchingItemIds.Count > 0 && !matchingItemIds.Contains(item.ItemId))
            {
                continue;
            }

            if (!duplicatesCount && await HasPreviousItemContributionAsync(participation, rule, item.ItemId, cancellationToken))
            {
                continue;
            }

            valueAdded += item.Quantity;
            matchedItems.Add(new
            {
                itemId = item.ItemId,
                itemName = item.ItemName,
                quantity = item.Quantity
            });
        }

        if (valueAdded <= 0)
        {
            return RuleEvaluationResult.NoMatch;
        }

        return new RuleEvaluationResult(
            Matched: true,
            ValueAdded: valueAdded,
            Description: $"Matched {valueAdded:0.##} item drop(s).",
            MetadataJson: JsonSerializer.Serialize(new
            {
                ruleType = RuleTypes.ItemCount,
                items = matchedItems
            }, JsonOptions));
    }

    private static RuleEvaluationResult EvaluatePointThreshold(
        IReadOnlyCollection<ActivityEventItem> activityItems,
        JsonElement config)
    {
        if (!config.TryGetProperty("pointsTable", out var pointsTable) ||
            pointsTable.ValueKind != JsonValueKind.Array)
        {
            return RuleEvaluationResult.NoMatch;
        }

        var totalPoints = 0m;
        var matchedItems = new List<object>();

        foreach (var item in activityItems)
        {
            foreach (var pointsEntry in pointsTable.EnumerateArray())
            {
                if (!pointsEntry.TryGetProperty("itemId", out var itemIdElement) ||
                    itemIdElement.GetInt32() != item.ItemId ||
                    !pointsEntry.TryGetProperty("points", out var pointsElement))
                {
                    continue;
                }

                var points = pointsElement.GetDecimal();
                var added = points * item.Quantity;
                totalPoints += added;
                matchedItems.Add(new
                {
                    itemId = item.ItemId,
                    itemName = item.ItemName,
                    points,
                    quantity = item.Quantity,
                    valueAdded = added
                });
                break;
            }
        }

        if (totalPoints <= 0)
        {
            return RuleEvaluationResult.NoMatch;
        }

        return new RuleEvaluationResult(
            Matched: true,
            ValueAdded: totalPoints,
            Description: $"Matched {totalPoints:0.##} point(s).",
            MetadataJson: JsonSerializer.Serialize(new
            {
                ruleType = RuleTypes.PointThreshold,
                items = matchedItems
            }, JsonOptions));
    }

    private async Task ApplyContributionAsync(
        ActivityEvent activity,
        ParticipationContext participation,
        TileRule rule,
        RuleEvaluationResult evaluation,
        CancellationToken cancellationToken)
    {
        var existingContribution = await dbContext.EventProgressContributions
            .AnyAsync(
                contribution => contribution.EventId == participation.EventId &&
                    contribution.TileId == rule.TileId &&
                    contribution.RuleId == rule.Id &&
                    contribution.ActivityEventId == activity.Id,
                cancellationToken);

        if (existingContribution)
        {
            return;
        }

        long? progressTeamId = rule.Scope == RuleScopes.Team ? participation.TeamId : null;
        long? progressPlayerId = rule.Scope == RuleScopes.Player ? participation.PlayerId : null;
        var now = clock.UtcNow;

        dbContext.EventProgressContributions.Add(new EventProgressContribution
        {
            EventId = participation.EventId,
            TileId = rule.TileId,
            TileTierId = rule.TileTierId,
            RuleId = rule.Id,
            TeamId = progressTeamId,
            PlayerId = participation.PlayerId,
            ActivityEventId = activity.Id,
            ValueAdded = evaluation.ValueAdded,
            Description = evaluation.Description,
            CreatedAt = now,
            MetadataJson = evaluation.MetadataJson
        });

        var tileProgress = await FindTileProgressAsync(
            participation.EventId,
            rule.TileId,
            progressTeamId,
            progressPlayerId,
            cancellationToken);

        if (tileProgress is null)
        {
            tileProgress = new EventTileProgress
            {
                EventId = participation.EventId,
                TileId = rule.TileId,
                TeamId = progressTeamId,
                PlayerId = progressPlayerId,
                UpdatedAt = now
            };
            dbContext.EventTileProgress.Add(tileProgress);
        }

        tileProgress.CurrentValue += evaluation.ValueAdded;
        tileProgress.UpdatedAt = now;

        if (rule.TileTierId.HasValue)
        {
            var tier = await dbContext.BingoTileTiers
                .SingleAsync(candidate => candidate.Id == rule.TileTierId.Value, cancellationToken);

            var tierProgress = await FindTileTierProgressAsync(
                participation.EventId,
                rule.TileId,
                tier.Id,
                progressTeamId,
                progressPlayerId,
                cancellationToken);

            if (tierProgress is null)
            {
                tierProgress = new EventTileTierProgress
                {
                    EventId = participation.EventId,
                    TileId = rule.TileId,
                    TileTierId = tier.Id,
                    TeamId = progressTeamId,
                    PlayerId = progressPlayerId,
                    UpdatedAt = now
                };
                dbContext.EventTileTierProgress.Add(tierProgress);
            }

            tierProgress.CurrentValue += evaluation.ValueAdded;
            tierProgress.UpdatedAt = now;

            await dbContext.SaveChangesAsync(cancellationToken);

            await new TierProgressScoringService(dbContext).RecalculateTileScopeAsync(
                participation.EventId,
                rule.TileId,
                progressTeamId,
                progressPlayerId,
                now,
                cancellationToken);
        }
    }

    private async Task<EventTileProgress?> FindTileProgressAsync(
        long eventId,
        long tileId,
        long? teamId,
        long? playerId,
        CancellationToken cancellationToken)
    {
        return await dbContext.EventTileProgress
            .SingleOrDefaultAsync(
                progress => progress.EventId == eventId &&
                    progress.TileId == tileId &&
                    progress.TeamId == teamId &&
                    progress.PlayerId == playerId,
                cancellationToken);
    }

    private async Task<EventTileTierProgress?> FindTileTierProgressAsync(
        long eventId,
        long tileId,
        long tileTierId,
        long? teamId,
        long? playerId,
        CancellationToken cancellationToken)
    {
        return await dbContext.EventTileTierProgress
            .SingleOrDefaultAsync(
                progress => progress.EventId == eventId &&
                    progress.TileId == tileId &&
                    progress.TileTierId == tileTierId &&
                    progress.TeamId == teamId &&
                    progress.PlayerId == playerId,
                cancellationToken);
    }

    private async Task<HashSet<int>> GetConfiguredItemIdsAsync(
        JsonElement config,
        CancellationToken cancellationToken)
    {
        var itemIds = new HashSet<int>();

        if (config.TryGetProperty("itemIds", out var itemIdsElement) &&
            itemIdsElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var itemIdElement in itemIdsElement.EnumerateArray())
            {
                itemIds.Add(itemIdElement.GetInt32());
            }
        }

        if (config.TryGetProperty("itemGroupKey", out var groupKeyElement) &&
            groupKeyElement.ValueKind == JsonValueKind.String)
        {
            var groupKey = groupKeyElement.GetString();
            if (!string.IsNullOrWhiteSpace(groupKey))
            {
                var groupItemIds = await (
                        from groupDefinition in dbContext.ItemGroups
                        join groupItem in dbContext.ItemGroupItems on groupDefinition.Id equals groupItem.ItemGroupId
                        where groupDefinition.Key == groupKey
                        select groupItem.ItemId)
                    .ToListAsync(cancellationToken);

                foreach (var itemId in groupItemIds)
                {
                    itemIds.Add(itemId);
                }
            }
        }

        return itemIds;
    }

    private async Task<bool> HasPreviousItemContributionAsync(
        ParticipationContext participation,
        TileRule rule,
        int itemId,
        CancellationToken cancellationToken)
    {
        long? progressTeamId = rule.Scope == RuleScopes.Team ? participation.TeamId : null;
        long? progressPlayerId = rule.Scope == RuleScopes.Player ? participation.PlayerId : null;

        var contributions = dbContext.EventProgressContributions
            .Where(contribution => contribution.EventId == participation.EventId &&
                contribution.TileId == rule.TileId &&
                contribution.RuleId == rule.Id);

        contributions = rule.Scope switch
        {
            RuleScopes.Team => contributions.Where(contribution => contribution.TeamId == progressTeamId),
            RuleScopes.Player => contributions.Where(contribution => contribution.PlayerId == progressPlayerId),
            RuleScopes.Event => contributions.Where(contribution => contribution.TeamId == null),
            _ => contributions
        };

        var metadataRows = await contributions
            .Select(contribution => contribution.MetadataJson)
            .ToListAsync(cancellationToken);

        return metadataRows.Any(metadata => metadata.Contains($"\"itemId\":{itemId}", StringComparison.Ordinal));
    }

    private static bool MatchesActivityType(ActivityEvent activity, JsonElement config)
    {
        if (!config.TryGetProperty("activityType", out var activityTypeElement) ||
            activityTypeElement.ValueKind != JsonValueKind.String)
        {
            return true;
        }

        return string.Equals(
            activity.ActivityType,
            activityTypeElement.GetString(),
            StringComparison.OrdinalIgnoreCase);
    }

    private static bool MatchesSource(ActivityEvent activity, JsonElement config)
    {
        if (!config.TryGetProperty("source", out var sourceElement))
        {
            return true;
        }

        if (sourceElement.ValueKind == JsonValueKind.String)
        {
            return string.Equals(
                activity.Source,
                sourceElement.GetString(),
                StringComparison.OrdinalIgnoreCase);
        }

        if (sourceElement.ValueKind == JsonValueKind.Array)
        {
            return sourceElement
                .EnumerateArray()
                .Any(source => string.Equals(
                    activity.Source,
                    source.GetString(),
                    StringComparison.OrdinalIgnoreCase));
        }

        return true;
    }

    private static JsonDocument ParseRuleConfig(string configJson)
    {
        if (string.IsNullOrWhiteSpace(configJson))
        {
            return JsonDocument.Parse("{}");
        }

        return JsonDocument.Parse(configJson);
    }

    private async Task<IDbContextTransaction?> BeginTransactionIfRelationalAsync(CancellationToken cancellationToken)
    {
        if (!dbContext.Database.IsRelational())
        {
            return null;
        }

        return await dbContext.Database.BeginTransactionAsync(cancellationToken);
    }

    private static TimeSpan GetRetryDelay(int attempts)
    {
        return attempts switch
        {
            <= 1 => TimeSpan.FromSeconds(10),
            2 => TimeSpan.FromMinutes(1),
            _ => TimeSpan.FromMinutes(5)
        };
    }

    private sealed record ParticipationContext(
        long EventId,
        long PlayerId,
        long? TeamId);

    private sealed record RuleEvaluationResult(
        bool Matched,
        decimal ValueAdded,
        string Description,
        string MetadataJson)
    {
        public static readonly RuleEvaluationResult NoMatch = new(
            Matched: false,
            ValueAdded: 0,
            Description: string.Empty,
            MetadataJson: "{}");
    }
}

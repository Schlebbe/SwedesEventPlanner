using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using SwedesEventPlanner.Application.Activity;
using SwedesEventPlanner.Application.Clock;
using SwedesEventPlanner.Contracts.Activity;
using SwedesEventPlanner.Domain.Activity;
using SwedesEventPlanner.Infrastructure.Persistence;

namespace SwedesEventPlanner.Infrastructure.Activity;

public sealed class ActivityIngestionService(
    EventPlannerDbContext dbContext,
    IClock clock) : IActivityIngestionService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<CreateActivityResponse> CreateActivityAsync(
        CreateActivityRequest request,
        CancellationToken cancellationToken)
    {
        ValidateRequest(request);

        var playerName = request.PlayerName.Trim();
        var normalizedPlayerName = playerName.ToLowerInvariant();

        var player = await dbContext.Players
            .SingleOrDefaultAsync(
                candidate => candidate.RuneScapeName.ToLower() == normalizedPlayerName,
                cancellationToken);

        if (player is null)
        {
            throw new ActivityIngestionException("Unknown player.");
        }

        var dedupeKey = string.IsNullOrWhiteSpace(request.DedupeKey)
            ? CreateGeneratedDedupeKey(player.Id, request)
            : request.DedupeKey.Trim();

        var existingActivity = await dbContext.ActivityEvents
            .AsNoTracking()
            .SingleOrDefaultAsync(activity => activity.DedupeKey == dedupeKey, cancellationToken);

        if (existingActivity is not null)
        {
            return new CreateActivityResponse(
                existingActivity.Id,
                null,
                Duplicate: true,
                Status: "duplicate");
        }

        await using var transaction = await BeginTransactionIfRelationalAsync(cancellationToken);

        var now = clock.UtcNow;
        var quantity = request.Quantity ?? 1;
        var activityEvent = new ActivityEvent
        {
            PlayerId = player.Id,
            ActivityType = request.ActivityType.Trim(),
            SourceSystem = "mock_dev",
            SourceEndpoint = "/api/activity",
            Source = request.Source?.Trim(),
            ItemId = request.ItemId,
            ItemName = request.ItemName?.Trim(),
            Quantity = request.ItemId.HasValue ? quantity : null,
            Skill = request.Skill?.Trim(),
            Xp = request.Xp,
            BossName = request.BossName?.Trim(),
            Kc = request.Kc,
            OccurredAt = request.OccurredAt,
            ReceivedAt = now,
            RawPayloadJson = JsonSerializer.Serialize(request, JsonOptions),
            DedupeKey = dedupeKey
        };

        dbContext.ActivityEvents.Add(activityEvent);
        await dbContext.SaveChangesAsync(cancellationToken);

        if (request.ActivityType == ActivityTypes.ItemDrop)
        {
            dbContext.ActivityEventItems.Add(new ActivityEventItem
            {
                ActivityEventId = activityEvent.Id,
                ItemId = request.ItemId!.Value,
                ItemName = request.ItemName?.Trim(),
                Quantity = quantity,
                Source = request.Source?.Trim()
            });
        }

        var queueItem = new ActivityProcessingQueueItem
        {
            ActivityEventId = activityEvent.Id,
            Status = ActivityProcessingStatuses.Pending,
            AvailableAt = now,
            CreatedAt = now
        };

        dbContext.ActivityProcessingQueue.Add(queueItem);
        await dbContext.SaveChangesAsync(cancellationToken);

        if (transaction is not null)
        {
            await transaction.CommitAsync(cancellationToken);
        }

        return new CreateActivityResponse(
            activityEvent.Id,
            queueItem.Id,
            Duplicate: false,
            Status: "queued");
    }

    private async Task<IDbContextTransaction?> BeginTransactionIfRelationalAsync(CancellationToken cancellationToken)
    {
        if (!dbContext.Database.IsRelational())
        {
            return null;
        }

        return await dbContext.Database.BeginTransactionAsync(cancellationToken);
    }

    private static void ValidateRequest(CreateActivityRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.PlayerName))
        {
            throw new ActivityIngestionException("Player name is required.");
        }

        if (string.IsNullOrWhiteSpace(request.ActivityType))
        {
            throw new ActivityIngestionException("Activity type is required.");
        }

        if (request.OccurredAt == default)
        {
            throw new ActivityIngestionException("OccurredAt is required.");
        }

        if (request.ActivityType != ActivityTypes.ItemDrop &&
            request.ActivityType != ActivityTypes.ManualTest)
        {
            throw new ActivityIngestionException("Unsupported activity type.");
        }

        if (request.ActivityType == ActivityTypes.ItemDrop)
        {
            if (!request.ItemId.HasValue)
            {
                throw new ActivityIngestionException("ItemId is required for item_drop activity.");
            }

            if (request.Quantity is <= 0)
            {
                throw new ActivityIngestionException("Quantity must be greater than zero.");
            }
        }
    }

    private static string CreateGeneratedDedupeKey(long playerId, CreateActivityRequest request)
    {
        var source = string.IsNullOrWhiteSpace(request.Source)
            ? "unknown"
            : request.Source.Trim().ToLowerInvariant();

        return FormattableString.Invariant(
            $"player:{playerId}:type:{request.ActivityType.Trim().ToLowerInvariant()}:source:{source}:item:{request.ItemId?.ToString() ?? "none"}:time:{request.OccurredAt.UtcDateTime:O}");
    }
}

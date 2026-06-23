using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SwedesEventPlanner.Application.Clock;
using SwedesEventPlanner.Contracts.Activity;
using SwedesEventPlanner.Domain.Activity;
using SwedesEventPlanner.Domain.Bingo;
using SwedesEventPlanner.Domain.Events;
using SwedesEventPlanner.Domain.Players;
using SwedesEventPlanner.Infrastructure.Activity;
using SwedesEventPlanner.Infrastructure.Persistence;

namespace SwedesEventPlanner.Application.Tests.Activity;

public sealed class ActivityProcessingServiceTests
{
    [Fact]
    public async Task Non_participant_player_does_not_progress_event()
    {
        await using var dbContext = CreateDbContext();
        var fixture = await SeedEventAsync(dbContext, includeParticipant: false);
        var activity = await AddItemActivityAsync(dbContext, fixture.Player.Id, itemId: 4151);
        var service = CreateProcessingService(dbContext);

        await service.ProcessActivityAsync(activity.Id, CancellationToken.None);

        Assert.Empty(dbContext.EventProgressContributions);
        Assert.Empty(dbContext.EventTileProgress);
    }

    [Fact]
    public async Task Inactive_event_does_not_progress()
    {
        await using var dbContext = CreateDbContext();
        var fixture = await SeedEventAsync(dbContext, eventStatus: EventStatuses.Scheduled);
        var activity = await AddItemActivityAsync(dbContext, fixture.Player.Id, itemId: 4151);
        var service = CreateProcessingService(dbContext);

        await service.ProcessActivityAsync(activity.Id, CancellationToken.None);

        Assert.Empty(dbContext.EventProgressContributions);
        Assert.Empty(dbContext.EventTileProgress);
    }

    [Fact]
    public async Task Activity_outside_event_time_window_does_not_progress()
    {
        await using var dbContext = CreateDbContext();
        var fixture = await SeedEventAsync(
            dbContext,
            startsAt: TestNow.AddHours(1),
            endsAt: TestNow.AddHours(2));
        var activity = await AddItemActivityAsync(dbContext, fixture.Player.Id, itemId: 4151, occurredAt: TestNow);
        var service = CreateProcessingService(dbContext);

        await service.ProcessActivityAsync(activity.Id, CancellationToken.None);

        Assert.Empty(dbContext.EventProgressContributions);
        Assert.Empty(dbContext.EventTileProgress);
    }

    [Fact]
    public async Task Team_scoped_rule_does_not_progress_player_without_team()
    {
        await using var dbContext = CreateDbContext();
        var fixture = await SeedEventAsync(dbContext, participantHasTeam: false);
        var activity = await AddItemActivityAsync(dbContext, fixture.Player.Id, itemId: 4151);
        var service = CreateProcessingService(dbContext);

        await service.ProcessActivityAsync(activity.Id, CancellationToken.None);

        Assert.Empty(dbContext.EventProgressContributions);
        Assert.Empty(dbContext.EventTileProgress);
    }

    [Fact]
    public async Task Item_count_rule_increments_progress()
    {
        await using var dbContext = CreateDbContext();
        var fixture = await SeedEventAsync(dbContext, ruleType: RuleTypes.ItemCount, required: 5);
        var firstActivity = await AddItemActivityAsync(dbContext, fixture.Player.Id, itemId: 4151, quantity: 2);
        var secondActivity = await AddItemActivityAsync(dbContext, fixture.Player.Id, itemId: 4151, quantity: 3);
        var service = CreateProcessingService(dbContext);

        await service.ProcessActivityAsync(firstActivity.Id, CancellationToken.None);
        await service.ProcessActivityAsync(secondActivity.Id, CancellationToken.None);

        var tileProgress = await dbContext.EventTileProgress.SingleAsync();
        var tierProgress = await dbContext.EventTileTierProgress.SingleAsync();

        Assert.Equal(5m, tileProgress.CurrentValue);
        Assert.Equal(5m, tierProgress.CurrentValue);
        Assert.True(tierProgress.IsAchieved);
        Assert.True(tierProgress.IsScored);
    }

    [Fact]
    public async Task Point_threshold_rule_accumulates_points()
    {
        await using var dbContext = CreateDbContext();
        var fixture = await SeedEventAsync(dbContext, ruleType: RuleTypes.PointThreshold, required: 10);
        var firstActivity = await AddItemActivityAsync(dbContext, fixture.Player.Id, itemId: 22486);
        var secondActivity = await AddItemActivityAsync(dbContext, fixture.Player.Id, itemId: 22324);
        var service = CreateProcessingService(dbContext);

        await service.ProcessActivityAsync(firstActivity.Id, CancellationToken.None);
        await service.ProcessActivityAsync(secondActivity.Id, CancellationToken.None);

        var tileProgress = await dbContext.EventTileProgress.SingleAsync();
        var tierProgress = await dbContext.EventTileTierProgress.SingleAsync();

        Assert.Equal(10m, tileProgress.CurrentValue);
        Assert.Equal(10m, tierProgress.CurrentValue);
        Assert.True(tierProgress.IsAchieved);
        Assert.True(tierProgress.IsScored);
    }

    [Fact]
    public async Task Tob_cumulative_point_tiers_score_first_tier_and_carry_progress_to_later_tiers()
    {
        await using var dbContext = CreateDbContext();
        var fixture = await SeedEventAsync(dbContext, ruleType: RuleTypes.PointThreshold, required: 10);
        var tile = await dbContext.BingoTiles.SingleAsync();
        await AddTileTierRuleAsync(dbContext, tile.Id, tierNumber: 2, RuleTypes.PointThreshold, required: 25);
        await AddTileTierRuleAsync(dbContext, tile.Id, tierNumber: 3, RuleTypes.PointThreshold, required: 50);
        var firstScythe = await AddItemActivityAsync(dbContext, fixture.Player.Id, itemId: 22486);
        var secondScythe = await AddItemActivityAsync(dbContext, fixture.Player.Id, itemId: 22486);
        var service = CreateProcessingService(dbContext);

        await service.ProcessActivityAsync(firstScythe.Id, CancellationToken.None);
        await service.ProcessActivityAsync(secondScythe.Id, CancellationToken.None);

        var tierRows = await GetTierProgressByTierNumberAsync(dbContext);
        Assert.Equal(14m, tierRows[1].CurrentValue);
        Assert.True(tierRows[1].IsScored);
        Assert.Equal(1, tierRows[1].ScoreAwarded);
        Assert.Equal(14m, tierRows[2].CurrentValue);
        Assert.False(tierRows[2].IsAchieved);
        Assert.False(tierRows[2].IsScored);
        Assert.Equal(14m, tierRows[3].CurrentValue);
        Assert.False(tierRows[3].IsScored);
        Assert.Equal(14m, (await dbContext.EventTileProgress.SingleAsync()).CurrentValue);
    }

    [Fact]
    public async Task Tob_cumulative_point_tiers_carry_over_to_tier_two_after_more_points()
    {
        await using var dbContext = CreateDbContext();
        var fixture = await SeedEventAsync(dbContext, ruleType: RuleTypes.PointThreshold, required: 10);
        var tile = await dbContext.BingoTiles.SingleAsync();
        await AddTileTierRuleAsync(dbContext, tile.Id, tierNumber: 2, RuleTypes.PointThreshold, required: 25);
        await AddTileTierRuleAsync(dbContext, tile.Id, tierNumber: 3, RuleTypes.PointThreshold, required: 50);
        var firstScythe = await AddItemActivityAsync(dbContext, fixture.Player.Id, itemId: 22486);
        var secondScythe = await AddItemActivityAsync(dbContext, fixture.Player.Id, itemId: 22486);
        var rapiers = await AddItemActivityAsync(dbContext, fixture.Player.Id, itemId: 22324, quantity: 4);
        var service = CreateProcessingService(dbContext);

        await service.ProcessActivityAsync(firstScythe.Id, CancellationToken.None);
        await service.ProcessActivityAsync(secondScythe.Id, CancellationToken.None);
        await service.ProcessActivityAsync(rapiers.Id, CancellationToken.None);

        var tierRows = await GetTierProgressByTierNumberAsync(dbContext);
        Assert.Equal(26m, tierRows[2].CurrentValue);
        Assert.True(tierRows[2].IsAchieved);
        Assert.True(tierRows[2].IsScored);
        Assert.Equal(26m, tierRows[3].CurrentValue);
        Assert.False(tierRows[3].IsScored);
        var tileProgress = await dbContext.EventTileProgress.SingleAsync();
        Assert.Equal(2, tileProgress.CurrentTier);
        Assert.Equal(26m, tileProgress.CurrentValue);
    }

    [Fact]
    public async Task Muspah_tier_progresses_before_zulrah_tier_but_scores_after_zulrah_scores()
    {
        await using var dbContext = CreateDbContext();
        var fixture = await SeedEventAsync(dbContext, ruleType: RuleTypes.ItemCount, required: 5);
        var tile = await dbContext.BingoTiles.SingleAsync();
        var zulrahRule = await dbContext.TileRules.SingleAsync();
        zulrahRule.ConfigJson = CreateItemCountConfig([12922], required: 5);
        await AddTileTierRuleAsync(
            dbContext,
            tile.Id,
            tierNumber: 2,
            RuleTypes.ItemCount,
            required: 5,
            itemIds: [27614]);
        await dbContext.SaveChangesAsync();
        var venatorShards = await AddItemActivityAsync(dbContext, fixture.Player.Id, itemId: 27614, quantity: 5);
        var service = CreateProcessingService(dbContext);

        await service.ProcessActivityAsync(venatorShards.Id, CancellationToken.None);

        var tierRowsAfterMuspah = await GetTierProgressByTierNumberAsync(dbContext);
        Assert.False(tierRowsAfterMuspah.ContainsKey(1));
        Assert.Equal(5m, tierRowsAfterMuspah[2].CurrentValue);
        Assert.True(tierRowsAfterMuspah[2].IsAchieved);
        Assert.False(tierRowsAfterMuspah[2].IsScored);

        var zulrahDrops = await AddItemActivityAsync(dbContext, fixture.Player.Id, itemId: 12922, quantity: 5);
        await service.ProcessActivityAsync(zulrahDrops.Id, CancellationToken.None);

        var tierRowsAfterZulrah = await GetTierProgressByTierNumberAsync(dbContext);
        Assert.Equal(5m, tierRowsAfterZulrah[1].CurrentValue);
        Assert.True(tierRowsAfterZulrah[1].IsScored);
        Assert.True(tierRowsAfterZulrah[2].IsScored);
        Assert.Equal(2, tierRowsAfterZulrah.Values.Sum(progress => progress.ScoreAwarded));
    }

    [Fact]
    public async Task Mixed_rule_types_keep_visible_tier_progress_separate_from_tile_summary()
    {
        await using var dbContext = CreateDbContext();
        var fixture = await SeedEventAsync(dbContext, ruleType: RuleTypes.ItemCount, required: 5);
        var tile = await dbContext.BingoTiles.SingleAsync();
        await AddTileTierRuleAsync(dbContext, tile.Id, tierNumber: 2, RuleTypes.PointThreshold, required: 25);
        var itemDrops = await AddItemActivityAsync(dbContext, fixture.Player.Id, itemId: 4151, quantity: 5);
        var scythes = await AddItemActivityAsync(dbContext, fixture.Player.Id, itemId: 22486, quantity: 2);
        var service = CreateProcessingService(dbContext);

        await service.ProcessActivityAsync(itemDrops.Id, CancellationToken.None);
        await service.ProcessActivityAsync(scythes.Id, CancellationToken.None);

        var tierRows = await GetTierProgressByTierNumberAsync(dbContext);
        Assert.Equal(5m, tierRows[1].CurrentValue);
        Assert.Equal(14m, tierRows[2].CurrentValue);
        Assert.Equal(1m, (await dbContext.EventTileProgress.SingleAsync()).CurrentValue);
    }

    [Fact]
    public async Task Later_tier_can_be_achieved_before_earlier_required_tier_but_scores_only_after_earlier_tier()
    {
        await using var dbContext = CreateDbContext();
        var fixture = await SeedEventAsync(dbContext, ruleType: RuleTypes.ItemCount, required: 1);
        var tile = await dbContext.BingoTiles.SingleAsync();
        var laterTier = new BingoTileTier
        {
            TileId = tile.Id,
            TierNumber = 2,
            Title = "Tier 2",
            ScoreValue = 3,
            SortOrder = 2,
            IsRequiredForBoardCompletion = true
        };
        dbContext.BingoTileTiers.Add(laterTier);
        await dbContext.SaveChangesAsync();
        dbContext.TileRules.Add(new TileRule
        {
            TileId = tile.Id,
            TileTierId = laterTier.Id,
            RuleType = RuleTypes.ItemCount,
            Scope = RuleScopes.Team,
            IsActive = true,
            ConfigJson = JsonSerializer.Serialize(new
            {
                activityType = ActivityTypes.ItemDrop,
                itemIds = new[] { 22486 },
                duplicatesCount = true,
                required = 1
            }),
            CreatedAt = TestNow
        });
        await dbContext.SaveChangesAsync();

        var service = CreateProcessingService(dbContext);
        var laterActivity = await AddItemActivityAsync(dbContext, fixture.Player.Id, itemId: 22486);
        await service.ProcessActivityAsync(laterActivity.Id, CancellationToken.None);

        var tierRowsAfterLater = await dbContext.EventTileTierProgress
            .OrderBy(progress => progress.TileTierId)
            .ToListAsync();
        var laterProgress = Assert.Single(tierRowsAfterLater);
        Assert.True(laterProgress.IsAchieved);
        Assert.False(laterProgress.IsScored);
        Assert.Equal(0, laterProgress.ScoreAwarded);

        var earlierActivity = await AddItemActivityAsync(dbContext, fixture.Player.Id, itemId: 4151);
        await service.ProcessActivityAsync(earlierActivity.Id, CancellationToken.None);

        var scoredRows = await dbContext.EventTileTierProgress
            .OrderBy(progress => progress.TileTierId)
            .ToListAsync();
        Assert.Equal(2, scoredRows.Count);
        Assert.All(scoredRows, progress => Assert.True(progress.IsScored));
        Assert.Equal(4, scoredRows.Sum(progress => progress.ScoreAwarded));
        Assert.True((await dbContext.EventTileProgress.SingleAsync()).IsCompleted);
    }

    [Fact]
    public async Task Duplicate_dedupe_key_does_not_double_count()
    {
        await using var dbContext = CreateDbContext();
        await SeedEventAsync(dbContext, ruleType: RuleTypes.ItemCount, required: 1);
        var ingestionService = new ActivityIngestionService(dbContext, new FixedClock(TestNow));
        var processingService = CreateProcessingService(dbContext);
        var request = new CreateActivityRequest
        {
            PlayerName = "Sebbe",
            ActivityType = ActivityTypes.ItemDrop,
            Source = "Abyssal demons",
            ItemId = 4151,
            ItemName = "Abyssal whip",
            Quantity = 1,
            OccurredAt = TestNow,
            DedupeKey = "phase3-duplicate-test"
        };

        var firstResponse = await ingestionService.CreateActivityAsync(request, CancellationToken.None);
        var secondResponse = await ingestionService.CreateActivityAsync(request, CancellationToken.None);

        await processingService.ProcessPendingActivityAsync(10, CancellationToken.None);

        Assert.False(firstResponse.Duplicate);
        Assert.True(secondResponse.Duplicate);
        Assert.Single(dbContext.ActivityEvents);
        Assert.Single(dbContext.EventProgressContributions);
        Assert.Equal(1m, (await dbContext.EventTileProgress.SingleAsync()).CurrentValue);
    }

    private static readonly DateTimeOffset TestNow = new(2026, 7, 2, 18, 30, 0, TimeSpan.Zero);

    private static EventPlannerDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<EventPlannerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .ConfigureWarnings(warnings => warnings.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        return new EventPlannerDbContext(options);
    }

    private static ActivityProcessingService CreateProcessingService(EventPlannerDbContext dbContext)
    {
        return new ActivityProcessingService(
            dbContext,
            new FixedClock(TestNow),
            NullLogger<ActivityProcessingService>.Instance);
    }

    private static async Task<TestFixture> SeedEventAsync(
        EventPlannerDbContext dbContext,
        bool includeParticipant = true,
        bool participantHasTeam = true,
        string eventStatus = EventStatuses.Active,
        string ruleType = RuleTypes.ItemCount,
        decimal required = 1,
        DateTimeOffset? startsAt = null,
        DateTimeOffset? endsAt = null)
    {
        var player = new Player
        {
            DisplayName = "Sebbe",
            RuneScapeName = "Sebbe",
            CreatedAt = TestNow
        };
        dbContext.Players.Add(player);

        var eventDefinition = new EventDefinition
        {
            Slug = Guid.NewGuid().ToString("N"),
            Name = "Test Event",
            EventType = "bingo",
            Status = eventStatus,
            StartsAt = startsAt ?? TestNow.AddHours(-1),
            EndsAt = endsAt ?? TestNow.AddHours(1),
            CreatedAt = TestNow
        };
        dbContext.Events.Add(eventDefinition);
        await dbContext.SaveChangesAsync();

        var team = new EventTeam
        {
            EventId = eventDefinition.Id,
            Name = "Blue",
            CreatedAt = TestNow
        };
        dbContext.EventTeams.Add(team);

        if (includeParticipant)
        {
            dbContext.EventParticipants.Add(new EventParticipant
            {
                EventId = eventDefinition.Id,
                PlayerId = player.Id,
                TeamId = participantHasTeam ? team.Id : null,
                JoinedAt = TestNow,
                Status = EventParticipantStatuses.Active
            });
        }

        var board = new BingoBoard
        {
            EventId = eventDefinition.Id,
            Name = "Board",
            CreatedAt = TestNow
        };
        dbContext.BingoBoards.Add(board);
        await dbContext.SaveChangesAsync();

        var tile = new BingoTile
        {
            BoardId = board.Id,
            Title = "Drops",
            SortOrder = 1
        };
        dbContext.BingoTiles.Add(tile);
        await dbContext.SaveChangesAsync();

        var tier = new BingoTileTier
        {
            TileId = tile.Id,
            TierNumber = 1,
            Title = "Tier 1",
            ScoreValue = 1,
            IsRequiredForBoardCompletion = true
        };
        dbContext.BingoTileTiers.Add(tier);
        await dbContext.SaveChangesAsync();

        dbContext.TileRules.Add(new TileRule
        {
            TileId = tile.Id,
            TileTierId = tier.Id,
            RuleType = ruleType,
            Scope = RuleScopes.Team,
            IsActive = true,
            ConfigJson = CreateRuleConfig(ruleType, required),
            CreatedAt = TestNow
        });

        await dbContext.SaveChangesAsync();
        return new TestFixture(player, eventDefinition, team);
    }

    private static string CreateRuleConfig(string ruleType, decimal required)
    {
        if (ruleType == RuleTypes.PointThreshold)
        {
            return JsonSerializer.Serialize(new
            {
                activityType = ActivityTypes.ItemDrop,
                source = new[] { "Theatre of Blood" },
                required,
                pointsTable = new[]
                {
                    new { itemId = 22486, name = "Scythe of vitur", points = 7 },
                    new { itemId = 22324, name = "Ghrazi rapier", points = 3 }
                }
            });
        }

        return JsonSerializer.Serialize(new
        {
            activityType = ActivityTypes.ItemDrop,
            itemIds = new[] { 4151 },
            duplicatesCount = true,
            required
        });
    }

    private static async Task<BingoTileTier> AddTileTierRuleAsync(
        EventPlannerDbContext dbContext,
        long tileId,
        int tierNumber,
        string ruleType,
        decimal required,
        IReadOnlyList<int>? itemIds = null)
    {
        var tier = new BingoTileTier
        {
            TileId = tileId,
            TierNumber = tierNumber,
            Title = $"Tier {tierNumber}",
            ScoreValue = 1,
            SortOrder = tierNumber,
            IsRequiredForBoardCompletion = true
        };
        dbContext.BingoTileTiers.Add(tier);
        await dbContext.SaveChangesAsync();

        dbContext.TileRules.Add(new TileRule
        {
            TileId = tileId,
            TileTierId = tier.Id,
            RuleType = ruleType,
            Scope = RuleScopes.Team,
            IsActive = true,
            ConfigJson = ruleType == RuleTypes.ItemCount
                ? CreateItemCountConfig(itemIds ?? [4151], required)
                : CreateRuleConfig(ruleType, required),
            CreatedAt = TestNow
        });
        await dbContext.SaveChangesAsync();
        return tier;
    }

    private static async Task<Dictionary<int, EventTileTierProgress>> GetTierProgressByTierNumberAsync(
        EventPlannerDbContext dbContext)
    {
        return await (
                from progress in dbContext.EventTileTierProgress
                join tier in dbContext.BingoTileTiers on progress.TileTierId equals tier.Id
                select new
                {
                    tier.TierNumber,
                    Progress = progress
                })
            .ToDictionaryAsync(row => row.TierNumber, row => row.Progress);
    }

    private static string CreateItemCountConfig(IReadOnlyList<int> itemIds, decimal required)
    {
        return JsonSerializer.Serialize(new
        {
            activityType = ActivityTypes.ItemDrop,
            itemIds,
            duplicatesCount = true,
            required
        });
    }

    private static async Task<ActivityEvent> AddItemActivityAsync(
        EventPlannerDbContext dbContext,
        long playerId,
        int itemId,
        int quantity = 1,
        DateTimeOffset? occurredAt = null)
    {
        var source = itemId is 22486 or 22324 ? "Theatre of Blood" : "Abyssal demons";
        var itemName = itemId switch
        {
            22486 => "Scythe of vitur",
            22324 => "Ghrazi rapier",
            4151 => "Abyssal whip",
            12922 => "Tanzanite fang",
            27614 => "Venator shard",
            _ => "Unknown item"
        };

        var activity = new ActivityEvent
        {
            PlayerId = playerId,
            ActivityType = ActivityTypes.ItemDrop,
            SourceSystem = "test",
            SourceEndpoint = "/api/activity",
            Source = source,
            ItemId = itemId,
            ItemName = itemName,
            Quantity = quantity,
            OccurredAt = occurredAt ?? TestNow,
            ReceivedAt = TestNow,
            RawPayloadJson = "{}",
            DedupeKey = Guid.NewGuid().ToString("N")
        };
        dbContext.ActivityEvents.Add(activity);
        await dbContext.SaveChangesAsync();

        dbContext.ActivityEventItems.Add(new ActivityEventItem
        {
            ActivityEventId = activity.Id,
            ItemId = itemId,
            ItemName = itemName,
            Quantity = quantity,
            Source = source
        });
        await dbContext.SaveChangesAsync();

        return activity;
    }

    private sealed record TestFixture(
        Player Player,
        SwedesEventPlanner.Domain.Events.EventDefinition EventDefinition,
        EventTeam Team);

    private sealed class FixedClock(DateTimeOffset utcNow) : IClock
    {
        public DateTimeOffset UtcNow { get; } = utcNow;
    }
}

using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SwedesEventPlanner.Domain.Activity;
using SwedesEventPlanner.Domain.Bingo;
using SwedesEventPlanner.Domain.Events;
using SwedesEventPlanner.Domain.ExternalCompetitions;
using SwedesEventPlanner.Domain.Players;
using SwedesEventPlanner.Infrastructure.Events;
using SwedesEventPlanner.Infrastructure.Persistence;

namespace SwedesEventPlanner.Application.Tests.Events;

public sealed class EventReadServiceTests
{
    [Fact]
    public async Task List_events_returns_public_current_events_only()
    {
        await using var dbContext = CreateDbContext();
        await AddEventAsync(dbContext, "active-event", EventStatuses.Active);
        await AddEventAsync(dbContext, "scheduled-event", EventStatuses.Scheduled);
        await AddEventAsync(dbContext, "draft-event", EventStatuses.Draft);
        var service = new EventReadService(dbContext);

        var response = await service.ListEventsAsync(CancellationToken.None);

        Assert.Equal(["active-event", "scheduled-event"], response.Events.Select(item => item.Slug));
    }

    [Fact]
    public async Task Board_response_maps_tiles_tiers_and_team_progress()
    {
        await using var dbContext = CreateDbContext();
        var fixture = await SeedProgressAsync(dbContext);
        var service = new EventReadService(dbContext);

        var response = await service.GetBoardAsync(fixture.EventSlug, CancellationToken.None);

        Assert.NotNull(response);
        Assert.Equal(fixture.EventSlug, response.Event.Slug);
        Assert.Equal("Demo Board", response.Board.Name);
        var tile = Assert.Single(response.Board.Tiles);
        Assert.Equal("TOB", tile.Title);
        Assert.Equal(7m, Assert.Single(tile.TeamProgress).CurrentValue);

        var tier = Assert.Single(tile.Tiers);
        Assert.Equal(10m, tier.RequiredValue);
        Assert.Equal(1, tier.ScoreValue);
        var tierProgress = Assert.Single(tier.TeamProgress);
        Assert.Equal(fixture.TeamId, tierProgress.TeamId);
        Assert.Equal(7m, tierProgress.CurrentValue);
        Assert.False(tierProgress.IsScored);
    }

    [Fact]
    public async Task Contribution_feed_hides_internal_activity_rule_and_metadata_fields()
    {
        await using var dbContext = CreateDbContext();
        var fixture = await SeedProgressAsync(dbContext);
        var service = new EventReadService(dbContext);

        var response = await service.GetContributionsAsync(fixture.EventSlug, limit: 10, CancellationToken.None);

        Assert.NotNull(response);
        var contribution = Assert.Single(response.Contributions);
        Assert.Equal("Sebbe", contribution.PlayerName);
        Assert.Equal("Blue", contribution.TeamName);
        Assert.Equal("TOB", contribution.TileTitle);

        var json = JsonSerializer.Serialize(response, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.DoesNotContain("activityEventId", json);
        Assert.DoesNotContain("ruleId", json);
        Assert.DoesNotContain("metadataJson", json);
        Assert.DoesNotContain("configJson", json);
        Assert.DoesNotContain("dedupe", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Contribution_feed_shows_team_level_temple_sync_rows_without_player()
    {
        await using var dbContext = CreateDbContext();
        var fixture = await SeedProgressAsync(dbContext);
        var tile = await dbContext.BingoTiles.SingleAsync();
        var tier = await dbContext.BingoTileTiers.SingleAsync();
        var rule = await dbContext.TileRules.SingleAsync();
        dbContext.EventProgressContributions.Add(new EventProgressContribution
        {
            EventId = (await dbContext.Events.SingleAsync()).Id,
            TileId = tile.Id,
            TileTierId = tier.Id,
            RuleId = rule.Id,
            TeamId = fixture.TeamId,
            PlayerId = null,
            ActivityEventId = null,
            ValueAdded = 100,
            Description = "TempleOSRS sync adjustment.",
            CreatedAt = TestNow.AddMinutes(1),
            MetadataJson = """{"source":"external_competition_metric"}"""
        });
        await dbContext.SaveChangesAsync();
        var service = new EventReadService(dbContext);

        var response = await service.GetContributionsAsync(fixture.EventSlug, limit: 10, CancellationToken.None);

        Assert.NotNull(response);
        var templeContribution = response.Contributions.First();
        Assert.Equal("TempleOSRS sync", templeContribution.PlayerName);
        Assert.Equal("Blue", templeContribution.TeamName);
        Assert.Equal(100m, templeContribution.ValueAdded);
    }

    [Fact]
    public async Task Board_response_includes_public_external_competition_refresh_availability()
    {
        await using var dbContext = CreateDbContext();
        var fixture = await SeedProgressAsync(dbContext);
        var eventDefinition = await dbContext.Events.SingleAsync();
        var nextAvailableAt = TestNow.AddMinutes(5);
        dbContext.ExternalCompetitions.Add(new ExternalCompetition
        {
            EventId = eventDefinition.Id,
            Provider = ExternalCompetitionProviders.TempleOsrs,
            ExternalId = "12345",
            Name = "TempleOSRS Competition",
            MetricType = "xp",
            MetricKey = "overall",
            Status = ExternalCompetitionStatuses.Active,
            LastSuccessfulSyncAt = TestNow,
            LastSyncStatus = ExternalCompetitionSyncRunStatuses.Succeeded,
            NextPublicSyncAvailableAt = nextAvailableAt,
            CreatedAt = TestNow
        });
        await dbContext.SaveChangesAsync();
        var service = new EventReadService(dbContext);

        var response = await service.GetBoardAsync(fixture.EventSlug, CancellationToken.None);

        Assert.NotNull(response);
        var freshness = Assert.Single(response.ExternalCompetitionFreshness);
        Assert.Equal(nextAvailableAt, freshness.NextPublicSyncAvailableAt);
    }

    private static readonly DateTimeOffset TestNow = new(2026, 7, 2, 18, 30, 0, TimeSpan.Zero);

    private static EventPlannerDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<EventPlannerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new EventPlannerDbContext(options);
    }

    private static async Task AddEventAsync(
        EventPlannerDbContext dbContext,
        string slug,
        string status)
    {
        dbContext.Events.Add(new EventDefinition
        {
            Slug = slug,
            Name = slug,
            EventType = "bingo",
            Status = status,
            StartsAt = TestNow.AddDays(-1),
            EndsAt = TestNow.AddDays(1),
            CreatedAt = TestNow
        });
        await dbContext.SaveChangesAsync();
    }

    private static async Task<ReadFixture> SeedProgressAsync(EventPlannerDbContext dbContext)
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
            Slug = "manual-bingo-2026",
            Name = "Manual Bingo 2026",
            EventType = "bingo",
            Status = EventStatuses.Active,
            StartsAt = TestNow.AddDays(-1),
            EndsAt = TestNow.AddDays(7),
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

        var board = new BingoBoard
        {
            EventId = eventDefinition.Id,
            Name = "Demo Board",
            CreatedAt = TestNow
        };
        dbContext.BingoBoards.Add(board);
        await dbContext.SaveChangesAsync();

        var tile = new BingoTile
        {
            BoardId = board.Id,
            Title = "TOB",
            Description = "Earn Theatre of Blood points.",
            SortOrder = 1
        };
        dbContext.BingoTiles.Add(tile);
        await dbContext.SaveChangesAsync();

        var tier = new BingoTileTier
        {
            TileId = tile.Id,
            TierNumber = 1,
            Title = "TOB Tier 1",
            ScoreValue = 1,
            IsRequiredForBoardCompletion = true
        };
        dbContext.BingoTileTiers.Add(tier);
        await dbContext.SaveChangesAsync();

        var rule = new TileRule
        {
            TileId = tile.Id,
            TileTierId = tier.Id,
            RuleType = RuleTypes.PointThreshold,
            Scope = RuleScopes.Team,
            IsActive = true,
            ConfigJson = """{"activityType":"item_drop","required":10}""",
            CreatedAt = TestNow
        };
        dbContext.TileRules.Add(rule);

        var activity = new ActivityEvent
        {
            PlayerId = player.Id,
            ActivityType = ActivityTypes.ItemDrop,
            SourceSystem = "mock_dev",
            SourceEndpoint = "/api/activity",
            Source = "Theatre of Blood",
            ItemId = 22486,
            ItemName = "Scythe of vitur",
            Quantity = 1,
            OccurredAt = TestNow,
            ReceivedAt = TestNow,
            RawPayloadJson = "{}",
            DedupeKey = "read-model-test"
        };
        dbContext.ActivityEvents.Add(activity);
        await dbContext.SaveChangesAsync();

        dbContext.EventTileProgress.Add(new EventTileProgress
        {
            EventId = eventDefinition.Id,
            TileId = tile.Id,
            TeamId = team.Id,
            PlayerId = null,
            CurrentValue = 7,
            CurrentTier = 0,
            IsCompleted = false,
            UpdatedAt = TestNow
        });
        dbContext.EventTileTierProgress.Add(new EventTileTierProgress
        {
            EventId = eventDefinition.Id,
            TileId = tile.Id,
            TileTierId = tier.Id,
            TeamId = team.Id,
            PlayerId = null,
            CurrentValue = 7,
            IsAchieved = false,
            IsScored = false,
            ScoreAwarded = 0,
            UpdatedAt = TestNow
        });
        dbContext.EventProgressContributions.Add(new EventProgressContribution
        {
            EventId = eventDefinition.Id,
            TileId = tile.Id,
            TileTierId = tier.Id,
            RuleId = rule.Id,
            TeamId = team.Id,
            PlayerId = player.Id,
            ActivityEventId = activity.Id,
            ValueAdded = 7,
            Description = "Matched 7 point(s).",
            CreatedAt = TestNow,
            MetadataJson = """{"itemId":22486,"dedupe":"hidden"}"""
        });

        await dbContext.SaveChangesAsync();
        return new ReadFixture(eventDefinition.Slug, team.Id);
    }

    private sealed record ReadFixture(string EventSlug, long TeamId);
}

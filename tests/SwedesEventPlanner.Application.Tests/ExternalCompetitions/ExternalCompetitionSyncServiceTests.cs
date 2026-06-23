using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SwedesEventPlanner.Application.Clock;
using SwedesEventPlanner.Application.ExternalCompetitions;
using SwedesEventPlanner.Contracts.Admin;
using SwedesEventPlanner.Domain.Activity;
using SwedesEventPlanner.Domain.Bingo;
using SwedesEventPlanner.Domain.Events;
using SwedesEventPlanner.Domain.ExternalCompetitions;
using SwedesEventPlanner.Domain.Players;
using SwedesEventPlanner.Infrastructure.Activity;
using SwedesEventPlanner.Infrastructure.Events;
using SwedesEventPlanner.Infrastructure.ExternalCompetitions;
using SwedesEventPlanner.Infrastructure.Persistence;

namespace SwedesEventPlanner.Application.Tests.ExternalCompetitions;

public sealed class ExternalCompetitionSyncServiceTests
{
    [Fact]
    public async Task Sync_caches_player_metrics_and_unmatched_identities()
    {
        await using var dbContext = CreateDbContext();
        var fixture = await SeedEventAsync(dbContext);
        var client = new FakeTempleClient(IndividualInfo([
            Participant("Sebbe", 100),
            Participant("Ghost", 250)
        ]));
        var service = CreateService(dbContext, client);
        var competition = await LinkCompetitionAsync(service, fixture);

        await service.SyncCompetitionAsync(fixture.Event.Slug, competition.Id, CancellationToken.None);

        Assert.Equal(2, await dbContext.ExternalCompetitionMetrics.CountAsync());
        Assert.Single(await dbContext.ExternalCompetitionMetrics.Where(metric => metric.PlayerId == fixture.Player.Id).ToListAsync());
        var unmatched = await service.ListUnmatchedIdentitiesAsync(competition.Id, CancellationToken.None);
        var identity = Assert.Single(unmatched.Identities);
        Assert.Equal("ghost", identity.RuneScapeName);
    }

    [Fact]
    public async Task Sync_caches_team_metrics()
    {
        await using var dbContext = CreateDbContext();
        var fixture = await SeedEventAsync(dbContext);
        var client = new FakeTempleClient(TeamInfo([
            Team("1", "Blue", 500, ["Sebbe"]),
            Team("2", "Temple Only", 900, ["Ghost"])
        ]));
        var service = CreateService(dbContext, client);
        var competition = await LinkCompetitionAsync(service, fixture);

        await service.SyncCompetitionAsync(fixture.Event.Slug, competition.Id, CancellationToken.None);

        var metrics = await service.ListTeamMetricsAsync(competition.Id, CancellationToken.None);
        Assert.Equal(2, metrics.Metrics.Count);
        Assert.Contains(metrics.Metrics, metric => metric.TeamName == "Blue" && metric.LocalTeamId == fixture.Team.Id);
        Assert.Contains(metrics.Metrics, metric => metric.TeamName == "Temple Only" && metric.HasLocalTeamMismatch);
    }

    [Fact]
    public async Task Unmatched_temple_names_do_not_count_for_non_team_local_team_grouped_scoring()
    {
        await using var dbContext = CreateDbContext();
        var fixture = await SeedEventAsync(dbContext);
        var client = new FakeTempleClient(IndividualInfo([
            Participant("Sebbe", 100),
            Participant("Ghost", 1000)
        ]));
        var service = CreateService(dbContext, client);
        var competition = await LinkCompetitionAsync(service, fixture);
        await AddExternalMetricRuleAsync(dbContext, fixture, competition.Id, required: 100);

        await service.SyncCompetitionAsync(fixture.Event.Slug, competition.Id, CancellationToken.None);

        var progress = await dbContext.EventTileProgress.SingleAsync();
        Assert.Equal(fixture.Team.Id, progress.TeamId);
        Assert.Equal(100m, progress.CurrentValue);
    }

    [Fact]
    public async Task Team_based_temple_competition_scores_from_cached_team_totals()
    {
        await using var dbContext = CreateDbContext();
        var fixture = await SeedEventAsync(dbContext);
        var client = new FakeTempleClient(TeamInfo(
            [Team("1", "Blue", 500, ["Ghost"])],
            [Participant("Ghost", 1)]));
        var service = CreateService(dbContext, client);
        var competition = await LinkCompetitionAsync(service, fixture);
        await AddExternalMetricRuleAsync(dbContext, fixture, competition.Id, required: 500);

        await service.SyncCompetitionAsync(fixture.Event.Slug, competition.Id, CancellationToken.None);

        var progress = await dbContext.EventTileProgress.SingleAsync();
        Assert.Equal(500m, progress.CurrentValue);
        Assert.True(progress.IsCompleted);
        Assert.Equal(500, Assert.Single(dbContext.ExternalCompetitionTeamMetrics).GainedValue);
    }

    [Fact]
    public async Task Non_team_temple_competition_scores_from_player_gains_grouped_by_local_teams()
    {
        await using var dbContext = CreateDbContext();
        var fixture = await SeedEventAsync(dbContext);
        var client = new FakeTempleClient(IndividualInfo([
            Participant("Sebbe", 100),
            Participant("Alicia", 40)
        ]));
        var service = CreateService(dbContext, client);
        var competition = await LinkCompetitionAsync(service, fixture);
        await AddExternalMetricRuleAsync(dbContext, fixture, competition.Id, required: 140);

        await service.SyncCompetitionAsync(fixture.Event.Slug, competition.Id, CancellationToken.None);

        var progress = await dbContext.EventTileProgress.SingleAsync();
        Assert.Equal(140m, progress.CurrentValue);
        Assert.True(progress.IsCompleted);
    }

    [Fact]
    public async Task Failed_sync_creates_failed_sync_run()
    {
        await using var dbContext = CreateDbContext();
        var fixture = await SeedEventAsync(dbContext);
        var client = new FakeTempleClient(new InvalidOperationException("Temple unavailable"));
        var service = CreateService(dbContext, client);
        var competition = await LinkCompetitionAsync(service, fixture);

        var run = await service.SyncCompetitionAsync(fixture.Event.Slug, competition.Id, CancellationToken.None);

        Assert.NotNull(run);
        Assert.Equal(ExternalCompetitionSyncRunStatuses.Failed, run.Status);
        Assert.Equal(ExternalCompetitionSyncRunStatuses.Failed, (await dbContext.ExternalCompetitions.SingleAsync()).LastSyncStatus);
    }

    [Fact]
    public async Task Duplicate_sync_is_skipped_when_active_run_exists()
    {
        await using var dbContext = CreateDbContext();
        var fixture = await SeedEventAsync(dbContext);
        var client = new FakeTempleClient(IndividualInfo([]));
        var service = CreateService(dbContext, client);
        var competition = await LinkCompetitionAsync(service, fixture);
        dbContext.ExternalCompetitionSyncRuns.Add(new ExternalCompetitionSyncRun
        {
            ExternalCompetitionId = competition.Id,
            TriggerType = "test",
            StartedAt = TestNow,
            Status = ExternalCompetitionSyncRunStatuses.Running
        });
        await dbContext.SaveChangesAsync();

        var run = await service.SyncCompetitionAsync(fixture.Event.Slug, competition.Id, CancellationToken.None);

        Assert.NotNull(run);
        Assert.Equal(ExternalCompetitionSyncRunStatuses.SkippedAlreadyRunning, run.Status);
        Assert.Equal(0, client.CallCount);
    }

    [Fact]
    public async Task Public_refresh_triggers_read_only_sync_and_sets_cooldown()
    {
        await using var dbContext = CreateDbContext();
        var fixture = await SeedEventAsync(dbContext);
        var client = new FakeTempleClient(IndividualInfo([Participant("Sebbe", 100)]));
        var service = CreateService(dbContext, client);
        await LinkCompetitionAsync(service, fixture);

        var response = await service.RequestPublicRefreshAsync(fixture.Event.Slug, CancellationToken.None);

        Assert.NotNull(response);
        var competition = Assert.Single(response.Competitions);
        Assert.True(competition.RefreshRequested);
        Assert.Equal(ExternalCompetitionSyncRunStatuses.Succeeded, competition.Status);
        Assert.NotNull(competition.LastSuccessfulSyncAt);
        Assert.True(competition.NextRefreshAvailableAt > TestNow);
        Assert.Equal(1, client.CallCount);
    }

    [Fact]
    public async Task Public_refresh_respects_five_minute_cooldown()
    {
        await using var dbContext = CreateDbContext();
        var fixture = await SeedEventAsync(dbContext);
        var client = new FakeTempleClient(IndividualInfo([Participant("Sebbe", 100)]));
        var service = CreateService(dbContext, client);
        await LinkCompetitionAsync(service, fixture);

        await service.RequestPublicRefreshAsync(fixture.Event.Slug, CancellationToken.None);
        var second = await service.RequestPublicRefreshAsync(fixture.Event.Slug, CancellationToken.None);

        Assert.NotNull(second);
        var competition = Assert.Single(second.Competitions);
        Assert.False(competition.RefreshRequested);
        Assert.Contains("Refresh available", competition.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, client.CallCount);
    }

    [Fact]
    public async Task Public_refresh_uses_configured_cooldown_minutes()
    {
        await using var dbContext = CreateDbContext();
        var fixture = await SeedEventAsync(dbContext);
        var client = new FakeTempleClient(IndividualInfo([Participant("Sebbe", 100)]));
        var service = CreateService(dbContext, client, publicRefreshCooldownMinutes: 2);
        await LinkCompetitionAsync(service, fixture);

        var response = await service.RequestPublicRefreshAsync(fixture.Event.Slug, CancellationToken.None);

        Assert.NotNull(response);
        var competition = Assert.Single(response.Competitions);
        Assert.Equal(TestNow.AddMinutes(2), competition.NextRefreshAvailableAt);
    }

    [Fact]
    public async Task Public_refresh_does_not_start_duplicate_active_sync()
    {
        await using var dbContext = CreateDbContext();
        var fixture = await SeedEventAsync(dbContext);
        var client = new FakeTempleClient(IndividualInfo([Participant("Sebbe", 100)]));
        var service = CreateService(dbContext, client);
        var competition = await LinkCompetitionAsync(service, fixture);
        dbContext.ExternalCompetitionSyncRuns.Add(new ExternalCompetitionSyncRun
        {
            ExternalCompetitionId = competition.Id,
            TriggerType = "test",
            StartedAt = TestNow,
            Status = ExternalCompetitionSyncRunStatuses.Running
        });
        await dbContext.SaveChangesAsync();

        var response = await service.RequestPublicRefreshAsync(fixture.Event.Slug, CancellationToken.None);

        Assert.NotNull(response);
        var refresh = Assert.Single(response.Competitions);
        Assert.False(refresh.RefreshRequested);
        Assert.Equal(ExternalCompetitionSyncRunStatuses.SkippedAlreadyRunning, refresh.Status);
        Assert.Equal(0, client.CallCount);
    }

    [Fact]
    public async Task Link_competition_allows_same_temple_id_on_multiple_events()
    {
        await using var dbContext = CreateDbContext();
        var firstFixture = await SeedEventAsync(dbContext);
        var secondFixture = await SeedEventAsync(dbContext);
        var client = new FakeTempleClient(IndividualInfo([]));
        var service = CreateService(dbContext, client);

        var firstCompetition = await LinkCompetitionAsync(service, firstFixture);
        var secondCompetition = await LinkCompetitionAsync(service, secondFixture);

        Assert.NotEqual(firstCompetition.Id, secondCompetition.Id);
        Assert.Equal(
            2,
            await dbContext.ExternalCompetitions.CountAsync(competition =>
                competition.Provider == ExternalCompetitionProviders.TempleOsrs &&
                competition.ExternalId == "123"));
    }

    [Fact]
    public async Task Event_reading_uses_cached_progress_without_calling_temple()
    {
        await using var dbContext = CreateDbContext();
        var fixture = await SeedEventAsync(dbContext);
        var client = new FakeTempleClient(IndividualInfo([Participant("Sebbe", 100)]));
        var service = CreateService(dbContext, client);
        var competition = await LinkCompetitionAsync(service, fixture);
        await AddExternalMetricRuleAsync(dbContext, fixture, competition.Id, required: 100);
        await service.SyncCompetitionAsync(fixture.Event.Slug, competition.Id, CancellationToken.None);

        var readService = new EventReadService(dbContext);
        var board = await readService.GetBoardAsync(fixture.Event.Slug, CancellationToken.None);

        Assert.NotNull(board);
        Assert.Equal(1, client.CallCount);
        Assert.Equal(100m, board.Board.Tiles.Single().TeamProgress.Single().CurrentValue);
        Assert.NotNull(Assert.Single(board.ExternalCompetitionFreshness).LastSuccessfulSyncAt);
    }

    [Fact]
    public async Task External_metric_tier_progress_does_not_overwrite_item_count_progress_on_same_tile()
    {
        await using var dbContext = CreateDbContext();
        var fixture = await SeedEventAsync(dbContext);
        var client = new FakeTempleClient(TeamInfo([Team("1", "Blue", 100, ["Sebbe"])]));
        var syncService = CreateService(dbContext, client);
        var competition = await LinkCompetitionAsync(syncService, fixture);
        await AddItemCountRuleAsync(dbContext, fixture);
        var externalTier = await AddTileTierAsync(dbContext, fixture.Tile.Id, tierNumber: 2);
        await AddExternalMetricRuleAsync(dbContext, fixture, competition.Id, required: 100, externalTier);
        var activity = await AddItemActivityAsync(dbContext, fixture.Player.Id, itemId: 4151, quantity: 5);
        var activityService = new ActivityProcessingService(
            dbContext,
            new FixedClock(TestNow),
            NullLogger<ActivityProcessingService>.Instance);

        await activityService.ProcessActivityAsync(activity.Id, CancellationToken.None);
        await syncService.SyncCompetitionAsync(fixture.Event.Slug, competition.Id, CancellationToken.None);

        var tierRows = await (
                from progress in dbContext.EventTileTierProgress
                join tier in dbContext.BingoTileTiers on progress.TileTierId equals tier.Id
                select new
                {
                    tier.TierNumber,
                    Progress = progress
                })
            .ToDictionaryAsync(row => row.TierNumber, row => row.Progress);
        Assert.Equal(5m, tierRows[1].CurrentValue);
        Assert.True(tierRows[1].IsScored);
        Assert.Equal(100m, tierRows[2].CurrentValue);
        Assert.True(tierRows[2].IsScored);
        Assert.Equal(2m, (await dbContext.EventTileProgress.SingleAsync()).CurrentValue);
        Assert.Equal(
            100m,
            await dbContext.EventProgressContributions
                .Where(contribution => contribution.TileTierId == externalTier.Id)
                .Select(contribution => contribution.ValueAdded)
                .SingleAsync());
    }

    private static readonly DateTimeOffset TestNow = new(2026, 7, 2, 18, 30, 0, TimeSpan.Zero);

    private static EventPlannerDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<EventPlannerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new EventPlannerDbContext(options);
    }

    private static ExternalCompetitionSyncService CreateService(
        EventPlannerDbContext dbContext,
        FakeTempleClient client,
        int publicRefreshCooldownMinutes = 5)
    {
        return new ExternalCompetitionSyncService(
            dbContext,
            client,
            new FixedClock(TestNow),
            Options.Create(new TempleOsrsOptions
            {
                PublicRefreshCooldownMinutes = publicRefreshCooldownMinutes
            }),
            NullLogger<ExternalCompetitionSyncService>.Instance);
    }

    private static async Task<AdminExternalCompetitionResponse> LinkCompetitionAsync(
        ExternalCompetitionSyncService service,
        TestFixture fixture)
    {
        var competition = await service.LinkTempleCompetitionAsync(
            fixture.Event.Slug,
            new LinkExternalCompetitionRequest
            {
                ExternalId = "123",
                Name = "Temple Test",
                MetricType = "xp",
                MetricKey = "attack"
            },
            CancellationToken.None);
        Assert.NotNull(competition);
        return competition;
    }

    private static async Task<TestFixture> SeedEventAsync(EventPlannerDbContext dbContext)
    {
        var player = new Player
        {
            DisplayName = "Sebbe",
            RuneScapeName = "Sebbe",
            CreatedAt = TestNow
        };
        var secondPlayer = new Player
        {
            DisplayName = "Alicia",
            RuneScapeName = "Alicia",
            CreatedAt = TestNow
        };
        dbContext.Players.AddRange(player, secondPlayer);

        var eventDefinition = new EventDefinition
        {
            Slug = Guid.NewGuid().ToString("N"),
            Name = "Temple Event",
            EventType = EventTypes.Bingo,
            Status = EventStatuses.Active,
            StartsAt = TestNow.AddDays(-1),
            EndsAt = TestNow.AddDays(1),
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
        await dbContext.SaveChangesAsync();

        dbContext.EventParticipants.AddRange(
            new EventParticipant
            {
                EventId = eventDefinition.Id,
                PlayerId = player.Id,
                TeamId = team.Id,
                JoinedAt = TestNow,
                Status = EventParticipantStatuses.Active
            },
            new EventParticipant
            {
                EventId = eventDefinition.Id,
                PlayerId = secondPlayer.Id,
                TeamId = team.Id,
                JoinedAt = TestNow,
                Status = EventParticipantStatuses.Active
            });

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
            Title = "Temple Tile",
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

        return new TestFixture(eventDefinition, player, team, tile, tier);
    }

    private static async Task AddExternalMetricRuleAsync(
        EventPlannerDbContext dbContext,
        TestFixture fixture,
        long externalCompetitionId,
        decimal required,
        BingoTileTier? tier = null)
    {
        var targetTier = tier ?? fixture.Tier;
        dbContext.TileRules.Add(new TileRule
        {
            TileId = fixture.Tile.Id,
            TileTierId = targetTier.Id,
            RuleType = RuleTypes.ExternalCompetitionMetric,
            Scope = RuleScopes.Team,
            IsActive = true,
            ConfigJson = JsonSerializer.Serialize(new
            {
                externalCompetitionId,
                provider = ExternalCompetitionProviders.TempleOsrs,
                metricType = "xp",
                metricKey = "attack",
                required
            }),
            CreatedAt = TestNow
        });
        await dbContext.SaveChangesAsync();
    }

    private static async Task AddItemCountRuleAsync(
        EventPlannerDbContext dbContext,
        TestFixture fixture)
    {
        dbContext.TileRules.Add(new TileRule
        {
            TileId = fixture.Tile.Id,
            TileTierId = fixture.Tier.Id,
            RuleType = RuleTypes.ItemCount,
            Scope = RuleScopes.Team,
            IsActive = true,
            ConfigJson = JsonSerializer.Serialize(new
            {
                activityType = ActivityTypes.ItemDrop,
                itemIds = new[] { 4151 },
                duplicatesCount = true,
                required = 5
            }),
            CreatedAt = TestNow
        });
        await dbContext.SaveChangesAsync();
    }

    private static async Task<BingoTileTier> AddTileTierAsync(
        EventPlannerDbContext dbContext,
        long tileId,
        int tierNumber)
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
        return tier;
    }

    private static async Task<ActivityEvent> AddItemActivityAsync(
        EventPlannerDbContext dbContext,
        long playerId,
        int itemId,
        int quantity)
    {
        var activity = new ActivityEvent
        {
            PlayerId = playerId,
            ActivityType = ActivityTypes.ItemDrop,
            SourceSystem = "test",
            SourceEndpoint = "/api/activity",
            Source = "Abyssal demons",
            ItemId = itemId,
            ItemName = "Abyssal whip",
            Quantity = quantity,
            OccurredAt = TestNow,
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
            ItemName = "Abyssal whip",
            Quantity = quantity,
            Source = "Abyssal demons"
        });
        await dbContext.SaveChangesAsync();
        return activity;
    }

    private static TempleOsrsCompetitionInfo IndividualInfo(
        IReadOnlyList<TempleOsrsParticipantMetric> participants)
    {
        return new TempleOsrsCompetitionInfo(
            "123",
            "Temple Test",
            IsTeamCompetition: false,
            "attack",
            "1",
            participants.Count,
            participants,
            []);
    }

    private static TempleOsrsCompetitionInfo TeamInfo(
        IReadOnlyList<TempleOsrsTeamMetric> teams,
        IReadOnlyList<TempleOsrsParticipantMetric>? participants = null)
    {
        return new TempleOsrsCompetitionInfo(
            "123",
            "Temple Test",
            IsTeamCompetition: true,
            "attack",
            "1",
            participants?.Count ?? 0,
            participants ?? [],
            teams);
    }

    private static TempleOsrsParticipantMetric Participant(string name, long gained)
    {
        return new TempleOsrsParticipantMetric(
            name,
            name,
            0,
            gained,
            gained,
            null,
            null,
            null,
            null,
            null,
            true,
            true);
    }

    private static TempleOsrsTeamMetric Team(
        string key,
        string name,
        long gained,
        IReadOnlyList<string> members)
    {
        return new TempleOsrsTeamMetric(
            key,
            name,
            0,
            gained,
            gained,
            null,
            null,
            members);
    }

    private sealed record TestFixture(
        EventDefinition Event,
        Player Player,
        EventTeam Team,
        BingoTile Tile,
        BingoTileTier Tier);

    private sealed class FakeTempleClient : ITempleOsrsClient
    {
        private readonly TempleOsrsCompetitionInfo? _response;
        private readonly Exception? _exception;

        public FakeTempleClient(TempleOsrsCompetitionInfo response)
        {
            _response = response;
        }

        public FakeTempleClient(Exception exception)
        {
            _exception = exception;
        }

        public int CallCount { get; private set; }

        public Task<TempleOsrsCompetitionInfo> GetCompetitionInfoAsync(
            string competitionId,
            CancellationToken cancellationToken)
        {
            CallCount++;
            if (_exception is not null)
            {
                throw _exception;
            }

            return Task.FromResult(_response!);
        }
    }

    private sealed class FixedClock(DateTimeOffset utcNow) : IClock
    {
        public DateTimeOffset UtcNow { get; } = utcNow;
    }
}

using Microsoft.EntityFrameworkCore;
using SwedesEventPlanner.Contracts.Admin;
using SwedesEventPlanner.Domain.Events;
using SwedesEventPlanner.Domain.Players;
using SwedesEventPlanner.Infrastructure.Admin;
using SwedesEventPlanner.Infrastructure.Persistence;

namespace SwedesEventPlanner.Application.Tests.Admin;

public sealed class AdminEventSetupServiceTests
{
    [Fact]
    public async Task Csv_import_creates_signups_players_and_participants()
    {
        await using var dbContext = CreateDbContext();
        await AddEventAsync(dbContext);
        var service = new AdminEventSetupService(dbContext);

        var response = await service.ImportCsvSignupsAsync(
            EventSlug,
            new CsvSignupImportRequest { CsvText = SignupCsv("Sebbe") },
            CancellationToken.None);

        Assert.Equal(1, response.RowsRead);
        Assert.Equal(1, response.SignupsCreated);
        Assert.Equal(1, response.PlayersCreated);
        Assert.Equal(1, response.ParticipantsCreated);
        Assert.Equal(0, response.InvalidRows);

        var signup = await dbContext.EventSignups.SingleAsync();
        Assert.Equal("Sebbe", signup.RuneScapeName);
        Assert.Equal("Weekends", signup.AvailabilityText);
        Assert.Equal(3m, signup.DailyHours);
        Assert.Equal("Raids", signup.PreferredContent);
        Assert.Equal("Blue", signup.TeamPreference);
        Assert.Equal("Ready", signup.Notes);
        Assert.NotNull(signup.EmailHash);
        Assert.DoesNotContain("player@example.invalid", signup.EmailHash, StringComparison.OrdinalIgnoreCase);

        var player = await dbContext.Players.SingleAsync();
        Assert.Equal("Sebbe", player.RuneScapeName);
        Assert.Equal("Sebbe", player.DisplayName);

        var participant = await dbContext.EventParticipants.SingleAsync();
        Assert.Equal(player.Id, participant.PlayerId);
        Assert.Null(participant.TeamId);
    }

    [Fact]
    public async Task Csv_import_matches_existing_player_without_copying_signup_fields_to_player()
    {
        await using var dbContext = CreateDbContext();
        await AddEventAsync(dbContext);
        dbContext.Players.Add(new Player
        {
            DisplayName = "Existing Sebbe",
            RuneScapeName = "Sebbe",
            CreatedAt = TestNow,
        });
        await dbContext.SaveChangesAsync();
        var service = new AdminEventSetupService(dbContext);

        await service.ImportCsvSignupsAsync(
            EventSlug,
            new CsvSignupImportRequest { CsvText = SignupCsv("sebbe") },
            CancellationToken.None);

        var player = await dbContext.Players.SingleAsync();
        Assert.Equal("Existing Sebbe", player.DisplayName);
        Assert.Equal("Sebbe", player.RuneScapeName);

        var signup = await dbContext.EventSignups.SingleAsync();
        Assert.Equal(player.Id, signup.PlayerId);
        Assert.Equal("Weekends", signup.AvailabilityText);
        Assert.Equal("Raids", signup.PreferredContent);
    }

    [Fact]
    public async Task Duplicate_import_updates_signup_without_duplicating_player_or_participant()
    {
        await using var dbContext = CreateDbContext();
        await AddEventAsync(dbContext);
        var service = new AdminEventSetupService(dbContext);
        var request = new CsvSignupImportRequest { CsvText = SignupCsv("Sebbe") };

        var first = await service.ImportCsvSignupsAsync(EventSlug, request, CancellationToken.None);
        var second = await service.ImportCsvSignupsAsync(EventSlug, request, CancellationToken.None);

        Assert.Equal(1, first.SignupsCreated);
        Assert.Equal(1, second.SignupsUpdated);
        Assert.Equal(0, second.SignupsCreated);
        Assert.Equal(0, second.PlayersCreated);
        Assert.Equal(1, second.ParticipantsUpdated);
        Assert.Equal(1, await dbContext.EventSignups.CountAsync());
        Assert.Equal(1, await dbContext.Players.CountAsync());
        Assert.Equal(1, await dbContext.EventParticipants.CountAsync());
    }

    [Fact]
    public async Task Csv_import_rejects_rows_without_runescape_name()
    {
        await using var dbContext = CreateDbContext();
        await AddEventAsync(dbContext);
        var service = new AdminEventSetupService(dbContext);

        var response = await service.ImportCsvSignupsAsync(
            EventSlug,
            new CsvSignupImportRequest
            {
                CsvText = "Timestamp,Email Address,RuneScape Name\n2026-07-02T17:30:00Z,player@example.invalid,"
            },
            CancellationToken.None);

        Assert.Equal(1, response.InvalidRows);
        Assert.Empty(dbContext.EventSignups);
        Assert.Empty(dbContext.EventParticipants);
        Assert.Empty(dbContext.Players);
    }

    [Fact]
    public async Task Team_creation_and_assignment_update_participant_roster()
    {
        await using var dbContext = CreateDbContext();
        await AddEventAsync(dbContext);
        var service = new AdminEventSetupService(dbContext);
        await service.ImportCsvSignupsAsync(
            EventSlug,
            new CsvSignupImportRequest { CsvText = SignupCsv("Sebbe") },
            CancellationToken.None);

        var beforeAssignment = await service.ListParticipantsAsync(EventSlug, CancellationToken.None);
        Assert.NotNull(beforeAssignment);
        Assert.Equal(1, beforeAssignment.UnassignedCount);

        var team = await service.CreateTeamAsync(
            EventSlug,
            new CreateEventTeamRequest { Name = "Blue" },
            CancellationToken.None);
        Assert.NotNull(team);

        var participant = await dbContext.EventParticipants.SingleAsync();
        var assigned = await service.AssignParticipantTeamAsync(
            EventSlug,
            participant.Id,
            new AssignParticipantTeamRequest { TeamId = team.Id },
            CancellationToken.None);

        Assert.NotNull(assigned);
        Assert.Equal(team.Id, assigned.TeamId);
        Assert.Equal("Blue", assigned.TeamName);
        Assert.False(assigned.IsUnassigned);

        var afterAssignment = await service.ListParticipantsAsync(EventSlug, CancellationToken.None);
        Assert.NotNull(afterAssignment);
        Assert.Equal(0, afterAssignment.UnassignedCount);
        Assert.Equal(1, Assert.Single(afterAssignment.Teams).ParticipantCount);
    }

    [Fact]
    public async Task Manual_setup_creates_event_board_tile_tier_and_rule()
    {
        await using var dbContext = CreateDbContext();
        var service = new AdminEventSetupService(dbContext);

        var eventDefinition = await service.CreateEventAsync(
            new CreateAdminEventRequest
            {
                Slug = "manual-bingo-2026",
                Name = "Manual Bingo 2026",
                EventType = EventTypes.Bingo,
                Status = EventStatuses.Active,
                StartsAt = TestNow,
                EndsAt = TestNow.AddDays(7),
                TimeZone = "Europe/Stockholm"
            },
            CancellationToken.None);
        var board = await service.CreateBoardAsync(
            eventDefinition.Slug,
            new CreateBingoBoardRequest { Name = "Manual Board", Rows = 5, Columns = 5 },
            CancellationToken.None);
        Assert.NotNull(board);
        var tile = await service.CreateTileAsync(
            eventDefinition.Slug,
            board.Id,
            new CreateBingoTileRequest
            {
                Title = "Scythe Drop",
                Description = "Manual activity smoke test.",
                SortOrder = 1
            },
            CancellationToken.None);
        Assert.NotNull(tile);
        var tier = await service.CreateTileTierAsync(
            eventDefinition.Slug,
            tile.Id,
            new CreateBingoTileTierRequest
            {
                TierNumber = 1,
                Title = "One drop",
                ScoreValue = 1,
                SortOrder = 1
            },
            CancellationToken.None);
        Assert.NotNull(tier);

        var rule = await service.CreateTileRuleAsync(
            eventDefinition.Slug,
            tile.Id,
            new UpsertTileRuleRequest
            {
                TileTierId = tier.Id,
                RuleType = "item_count",
                Scope = "team",
                ConfigJson = "{\"activityType\":\"item_drop\",\"itemIds\":[22486],\"requiredValue\":1}"
            },
            CancellationToken.None);

        Assert.NotNull(rule);
        Assert.Equal("item_count", rule.RuleType);
        Assert.Equal(tier.Id, rule.TileTierId);

        var setup = await service.GetBoardSetupAsync(eventDefinition.Slug, CancellationToken.None);
        Assert.NotNull(setup);
        Assert.Equal("Manual Board", setup.Board?.Name);
        var setupTile = Assert.Single(setup.Tiles);
        Assert.Equal("Scythe Drop", setupTile.Title);
        Assert.Single(setupTile.Tiers);
        Assert.Single(setupTile.Rules);
    }

    private const string EventSlug = "summer-bingo-2026";
    private static readonly DateTimeOffset TestNow = new(2026, 7, 2, 18, 30, 0, TimeSpan.Zero);

    private static EventPlannerDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<EventPlannerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new EventPlannerDbContext(options);
    }

    private static async Task AddEventAsync(EventPlannerDbContext dbContext)
    {
        dbContext.Events.Add(new EventDefinition
        {
            Slug = EventSlug,
            Name = "Summer Bingo 2026",
            EventType = EventTypes.Bingo,
            Status = EventStatuses.Active,
            StartsAt = TestNow.AddDays(-1),
            EndsAt = TestNow.AddDays(14),
            CreatedAt = TestNow,
        });

        await dbContext.SaveChangesAsync();
    }

    private static string SignupCsv(string runeScapeName)
    {
        return string.Join(
            Environment.NewLine,
            "Timestamp,Email Address,RuneScape Name,Availability,Daily Hours,Preferred Content,Notes,Team Preference",
            $"2026-07-02T17:30:00Z,player@example.invalid,{runeScapeName},Weekends,3,Raids,Ready,Blue");
    }
}

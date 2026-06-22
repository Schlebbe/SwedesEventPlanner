using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SwedesEventPlanner.Application.Admin;
using SwedesEventPlanner.Application.Clock;
using SwedesEventPlanner.Contracts.Admin;
using SwedesEventPlanner.Domain.Bingo;
using SwedesEventPlanner.Domain.Common;
using SwedesEventPlanner.Domain.Events;
using SwedesEventPlanner.Domain.ExternalCompetitions;
using SwedesEventPlanner.Domain.Players;
using SwedesEventPlanner.Infrastructure.Persistence;

namespace SwedesEventPlanner.Infrastructure.Admin;

public sealed class AdminDevSeedService(
    EventPlannerDbContext dbContext,
    IClock clock) : IAdminDevSeedService
{
    private const string DemoSlug = "local-mock-activity-demo";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<AdminDevSeedResponse> SeedMockActivityDemoAsync(CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var eventDefinition = await EnsureEventAsync(now, cancellationToken);
        var blue = await EnsureTeamAsync(eventDefinition.Id, "Blue", now, cancellationToken);
        var gold = await EnsureTeamAsync(eventDefinition.Id, "Gold", now, cancellationToken);
        var green = await EnsureTeamAsync(eventDefinition.Id, "Green", now, cancellationToken);

        var sebbe = await EnsurePlayerAsync("Sebbe", "Sebbe", now, cancellationToken);
        var alicia = await EnsurePlayerAsync("Alicia", "Alicia", now, cancellationToken);
        var oskar = await EnsurePlayerAsync("Oskar", "Oskar", now, cancellationToken);
        var linn = await EnsurePlayerAsync("Linn", "Linn", now, cancellationToken);
        var maja = await EnsurePlayerAsync("Maja", "Maja", now, cancellationToken);

        await EnsureParticipantAsync(eventDefinition.Id, sebbe.Id, blue.Id, now, cancellationToken);
        await EnsureParticipantAsync(eventDefinition.Id, alicia.Id, blue.Id, now, cancellationToken);
        await EnsureParticipantAsync(eventDefinition.Id, oskar.Id, gold.Id, now, cancellationToken);
        await EnsureParticipantAsync(eventDefinition.Id, linn.Id, green.Id, now, cancellationToken);
        await EnsureParticipantAsync(eventDefinition.Id, maja.Id, teamId: null, now, cancellationToken);

        var board = await EnsureBoardAsync(eventDefinition.Id, "Demo Board", now, cancellationToken);
        var tobTile = await EnsureTileAsync(board.Id, "TOB Points", "Earn Theatre of Blood points.", 1, now, cancellationToken);
        var petTile = await EnsureTileAsync(board.Id, "Pet Drops", "Count rare pet drops.", 2, now, cancellationToken);
        var supplyTile = await EnsureTileAsync(board.Id, "Slayer Supplies", "Collect useful PvM supplies.", 3, now, cancellationToken);
        var templeTile = await EnsureTileAsync(board.Id, "Temple XP", "Cached TempleOSRS XP gains.", 4, now, cancellationToken);

        var tobTier = await EnsureTierAsync(tobTile.Id, 1, "10 points", scoreValue: 2, now, cancellationToken);
        var petTier = await EnsureTierAsync(petTile.Id, 1, "First pet", scoreValue: 3, now, cancellationToken);
        var supplyTier = await EnsureTierAsync(supplyTile.Id, 1, "25 supplies", scoreValue: 1, now, cancellationToken);
        var templeTier = await EnsureTierAsync(templeTile.Id, 1, "1M XP", scoreValue: 2, now, cancellationToken);

        var tobRule = await EnsureRuleAsync(
            tobTile.Id,
            tobTier.Id,
            RuleTypes.PointThreshold,
            RuleScopes.Team,
            new
            {
                activityType = "item_drop",
                source = new[] { "Theatre of Blood" },
                required = 10,
                pointsTable = new[]
                {
                    new { itemId = 22477, name = "Avernic defender hilt", points = 1 },
                    new { itemId = 22324, name = "Ghrazi rapier", points = 3 },
                    new { itemId = 22481, name = "Sanguinesti staff", points = 3 },
                    new { itemId = 22486, name = "Scythe of vitur", points = 7 }
                }
            },
            now,
            cancellationToken);

        var petRule = await EnsureRuleAsync(
            petTile.Id,
            petTier.Id,
            RuleTypes.ItemCount,
            RuleScopes.Team,
            new
            {
                activityType = "item_drop",
                itemIds = new[] { 30000 },
                duplicatesCount = true,
                required = 1
            },
            now,
            cancellationToken);

        var supplyRule = await EnsureRuleAsync(
            supplyTile.Id,
            supplyTier.Id,
            RuleTypes.ItemCount,
            RuleScopes.Team,
            new
            {
                activityType = "item_drop",
                itemGroupKey = "slayer-supplies",
                duplicatesCount = true,
                required = 25
            },
            now,
            cancellationToken);

        var templeCompetition = await EnsureTemplePlaceholderAsync(eventDefinition.Id, now, cancellationToken);
        var templeRule = await EnsureRuleAsync(
            templeTile.Id,
            templeTier.Id,
            RuleTypes.ExternalCompetitionMetric,
            RuleScopes.Team,
            new
            {
                externalCompetitionId = templeCompetition.Id,
                provider = ExternalCompetitionProviders.TempleOsrs,
                metricType = "xp",
                metricKey = "overall",
                required = 1_000_000
            },
            now,
            cancellationToken);

        await ResetDemoProgressAsync(eventDefinition.Id, cancellationToken);
        await SeedProgressAsync(eventDefinition.Id, tobTile.Id, tobTier.Id, tobRule.Id, blue.Id, sebbe.Id, 7, 10, isCompleted: false, now.AddMinutes(-40), cancellationToken);
        await SeedProgressAsync(eventDefinition.Id, tobTile.Id, tobTier.Id, tobRule.Id, gold.Id, oskar.Id, 10, 10, isCompleted: true, now.AddMinutes(-35), cancellationToken);
        await SeedProgressAsync(eventDefinition.Id, petTile.Id, petTier.Id, petRule.Id, blue.Id, alicia.Id, 1, 1, isCompleted: true, now.AddMinutes(-30), cancellationToken);
        await SeedProgressAsync(eventDefinition.Id, supplyTile.Id, supplyTier.Id, supplyRule.Id, green.Id, linn.Id, 12, 25, isCompleted: false, now.AddMinutes(-25), cancellationToken);
        await SeedProgressAsync(eventDefinition.Id, templeTile.Id, templeTier.Id, templeRule.Id, blue.Id, sebbe.Id, 640_000, 1_000_000, isCompleted: false, now.AddMinutes(-20), cancellationToken);
        await SeedProgressAsync(eventDefinition.Id, templeTile.Id, templeTier.Id, templeRule.Id, gold.Id, oskar.Id, 1_250_000, 1_000_000, isCompleted: true, now.AddMinutes(-15), cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);

        return new AdminDevSeedResponse(
            sebbe.Id,
            eventDefinition.Id,
            blue.Id,
            tobTile.Id,
            petTile.Id,
            sebbe.RuneScapeName,
            eventDefinition.Slug,
            [
                """
                {"playerName":"Sebbe","activityType":"item_drop","source":"Theatre of Blood","itemId":22486,"itemName":"Scythe of vitur","quantity":1,"occurredAt":"2026-07-02T18:30:00Z","dedupeKey":"local-demo-scythe-1"}
                """,
                """
                {"playerName":"Alicia","activityType":"item_drop","source":"Pet Drop","itemId":30000,"itemName":"Tiny demo pet","quantity":1,"occurredAt":"2026-07-02T18:35:00Z","dedupeKey":"local-demo-pet-1"}
                """,
                """
                {"playerName":"Linn","activityType":"item_drop","source":"Slayer","itemId":3024,"itemName":"Super restore(4)","quantity":5,"occurredAt":"2026-07-02T18:40:00Z","dedupeKey":"local-demo-supplies-1"}
                """
            ]);
    }

    private async Task<EventDefinition> EnsureEventAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        var eventDefinition = await dbContext.Events
            .SingleOrDefaultAsync(candidate => candidate.Slug == DemoSlug, cancellationToken);

        if (eventDefinition is null)
        {
            eventDefinition = new EventDefinition
            {
                Slug = DemoSlug,
                Name = "Local Mock Activity Demo",
                EventType = EventTypes.Bingo,
                Status = EventStatuses.Active,
                StartsAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
                EndsAt = new DateTimeOffset(2026, 12, 31, 23, 59, 59, TimeSpan.Zero),
                CreatedAt = now
            };
            dbContext.Events.Add(eventDefinition);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        else
        {
            eventDefinition.Name = "Local Mock Activity Demo";
            eventDefinition.Status = EventStatuses.Active;
            eventDefinition.StartsAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
            eventDefinition.EndsAt = new DateTimeOffset(2026, 12, 31, 23, 59, 59, TimeSpan.Zero);
        }

        return eventDefinition;
    }

    private async Task<Player> EnsurePlayerAsync(
        string runeScapeName,
        string displayName,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var player = await dbContext.Players
            .SingleOrDefaultAsync(candidate => candidate.RuneScapeName == runeScapeName, cancellationToken);

        if (player is not null)
        {
            player.DisplayName = displayName;
            return player;
        }

        player = new Player
        {
            DisplayName = displayName,
            RuneScapeName = runeScapeName,
            CreatedAt = now
        };
        dbContext.Players.Add(player);
        await dbContext.SaveChangesAsync(cancellationToken);
        return player;
    }

    private async Task<EventTeam> EnsureTeamAsync(
        long eventId,
        string name,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var team = await dbContext.EventTeams
            .SingleOrDefaultAsync(candidate => candidate.EventId == eventId && candidate.Name == name, cancellationToken);

        if (team is not null)
        {
            return team;
        }

        team = new EventTeam
        {
            EventId = eventId,
            Name = name,
            CreatedAt = now
        };
        dbContext.EventTeams.Add(team);
        await dbContext.SaveChangesAsync(cancellationToken);
        return team;
    }

    private async Task EnsureParticipantAsync(
        long eventId,
        long playerId,
        long? teamId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var participant = await dbContext.EventParticipants
            .SingleOrDefaultAsync(candidate => candidate.EventId == eventId && candidate.PlayerId == playerId, cancellationToken);

        if (participant is null)
        {
            dbContext.EventParticipants.Add(new EventParticipant
            {
                EventId = eventId,
                PlayerId = playerId,
                TeamId = teamId,
                JoinedAt = now,
                Status = EventParticipantStatuses.Active
            });
            await dbContext.SaveChangesAsync(cancellationToken);
            return;
        }

        participant.TeamId = teamId;
        participant.Status = EventParticipantStatuses.Active;
    }

    private async Task<BingoBoard> EnsureBoardAsync(
        long eventId,
        string name,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var board = await dbContext.BingoBoards
            .SingleOrDefaultAsync(candidate => candidate.EventId == eventId && candidate.Name == name, cancellationToken);

        if (board is not null)
        {
            return board;
        }

        board = new BingoBoard
        {
            EventId = eventId,
            Name = name,
            Rows = 2,
            Columns = 2,
            CreatedAt = now
        };
        dbContext.BingoBoards.Add(board);
        await dbContext.SaveChangesAsync(cancellationToken);
        return board;
    }

    private async Task<BingoTile> EnsureTileAsync(
        long boardId,
        string title,
        string description,
        int sortOrder,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var tile = await dbContext.BingoTiles
            .SingleOrDefaultAsync(candidate => candidate.BoardId == boardId && candidate.Title == title, cancellationToken);

        if (tile is not null)
        {
            tile.Description = description;
            tile.SortOrder = sortOrder;
            return tile;
        }

        tile = new BingoTile
        {
            BoardId = boardId,
            Title = title,
            Description = description,
            SortOrder = sortOrder,
            ConfigJson = JsonDefaults.Object
        };
        dbContext.BingoTiles.Add(tile);
        await dbContext.SaveChangesAsync(cancellationToken);
        return tile;
    }

    private async Task<BingoTileTier> EnsureTierAsync(
        long tileId,
        int tierNumber,
        string title,
        int scoreValue,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var tier = await dbContext.BingoTileTiers
            .SingleOrDefaultAsync(candidate => candidate.TileId == tileId && candidate.TierNumber == tierNumber, cancellationToken);

        if (tier is not null)
        {
            tier.Title = title;
            tier.ScoreValue = scoreValue;
            tier.IsRequiredForBoardCompletion = true;
            return tier;
        }

        tier = new BingoTileTier
        {
            TileId = tileId,
            TierNumber = tierNumber,
            Title = title,
            ScoreValue = scoreValue,
            IsRequiredForBoardCompletion = true,
            ConfigJson = JsonDefaults.Object
        };
        dbContext.BingoTileTiers.Add(tier);
        await dbContext.SaveChangesAsync(cancellationToken);
        return tier;
    }

    private async Task<TileRule> EnsureRuleAsync(
        long tileId,
        long tileTierId,
        string ruleType,
        string scope,
        object config,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var rule = await dbContext.TileRules
            .SingleOrDefaultAsync(
                candidate => candidate.TileId == tileId &&
                    candidate.TileTierId == tileTierId &&
                    candidate.RuleType == ruleType,
                cancellationToken);

        var configJson = JsonSerializer.Serialize(config, JsonOptions);

        if (rule is null)
        {
            rule = new TileRule
            {
                TileId = tileId,
                TileTierId = tileTierId,
                RuleType = ruleType,
                Scope = scope,
                IsActive = true,
                ConfigJson = configJson,
                CreatedAt = now
            };
            dbContext.TileRules.Add(rule);
            await dbContext.SaveChangesAsync(cancellationToken);
            return rule;
        }

        rule.Scope = scope;
        rule.IsActive = true;
        rule.ConfigJson = configJson;
        return rule;
    }

    private async Task<ExternalCompetition> EnsureTemplePlaceholderAsync(
        long eventId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var competition = await dbContext.ExternalCompetitions
            .SingleOrDefaultAsync(
                candidate => candidate.EventId == eventId &&
                    candidate.Provider == ExternalCompetitionProviders.TempleOsrs &&
                    candidate.ExternalId == "demo-temple-readonly",
                cancellationToken);

        if (competition is not null)
        {
            competition.Name = "Demo TempleOSRS Read Cache";
            competition.MetricType = "xp";
            competition.MetricKey = "overall";
            competition.Status = ExternalCompetitionStatuses.Active;
            return competition;
        }

        competition = new ExternalCompetition
        {
            EventId = eventId,
            Provider = ExternalCompetitionProviders.TempleOsrs,
            ExternalId = "demo-temple-readonly",
            Name = "Demo TempleOSRS Read Cache",
            MetricType = "xp",
            MetricKey = "overall",
            CompetitionMode = ExternalCompetitionModes.Individual,
            Status = ExternalCompetitionStatuses.Active,
            LastSyncStatus = "demo_cached",
            LastSuccessfulSyncAt = now.AddMinutes(-10),
            LastSyncedAt = now.AddMinutes(-10),
            CreatedAt = now,
            ConfigJson = JsonSerializer.Serialize(new { source = "dev_seed", readOnly = true }, JsonOptions)
        };
        dbContext.ExternalCompetitions.Add(competition);
        await dbContext.SaveChangesAsync(cancellationToken);
        return competition;
    }

    private async Task ResetDemoProgressAsync(long eventId, CancellationToken cancellationToken)
    {
        var contributions = await dbContext.EventProgressContributions
            .Where(contribution => contribution.EventId == eventId)
            .ToListAsync(cancellationToken);
        var tileProgress = await dbContext.EventTileProgress
            .Where(progress => progress.EventId == eventId)
            .ToListAsync(cancellationToken);
        var tierProgress = await dbContext.EventTileTierProgress
            .Where(progress => progress.EventId == eventId)
            .ToListAsync(cancellationToken);

        dbContext.EventProgressContributions.RemoveRange(contributions);
        dbContext.EventTileProgress.RemoveRange(tileProgress);
        dbContext.EventTileTierProgress.RemoveRange(tierProgress);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task SeedProgressAsync(
        long eventId,
        long tileId,
        long tileTierId,
        long ruleId,
        long teamId,
        long playerId,
        decimal currentValue,
        decimal requiredValue,
        bool isCompleted,
        DateTimeOffset createdAt,
        CancellationToken cancellationToken)
    {
        dbContext.EventTileProgress.Add(new EventTileProgress
        {
            EventId = eventId,
            TileId = tileId,
            TeamId = teamId,
            PlayerId = null,
            CurrentValue = currentValue,
            CurrentTier = isCompleted ? 1 : 0,
            IsCompleted = isCompleted,
            CompletedAt = isCompleted ? createdAt : null,
            UpdatedAt = createdAt,
            MetadataJson = JsonSerializer.Serialize(new { source = "dev_seed" }, JsonOptions)
        });

        dbContext.EventTileTierProgress.Add(new EventTileTierProgress
        {
            EventId = eventId,
            TileId = tileId,
            TileTierId = tileTierId,
            TeamId = teamId,
            PlayerId = null,
            CurrentValue = currentValue,
            IsAchieved = isCompleted,
            AchievedAt = isCompleted ? createdAt : null,
            IsScored = isCompleted,
            ScoredAt = isCompleted ? createdAt : null,
            ScoreAwarded = isCompleted ? 1 : 0,
            UpdatedAt = createdAt,
            MetadataJson = JsonSerializer.Serialize(new { source = "dev_seed", requiredValue }, JsonOptions)
        });

        dbContext.EventProgressContributions.Add(new EventProgressContribution
        {
            EventId = eventId,
            TileId = tileId,
            TileTierId = tileTierId,
            RuleId = ruleId,
            TeamId = teamId,
            PlayerId = playerId,
            ActivityEventId = null,
            ValueAdded = currentValue,
            Description = isCompleted ? "Seeded completed demo progress." : "Seeded partial demo progress.",
            CreatedAt = createdAt,
            MetadataJson = JsonSerializer.Serialize(new { source = "dev_seed" }, JsonOptions)
        });

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}

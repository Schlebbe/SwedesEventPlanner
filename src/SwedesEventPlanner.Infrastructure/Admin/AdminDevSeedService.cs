using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SwedesEventPlanner.Application.Admin;
using SwedesEventPlanner.Application.Clock;
using SwedesEventPlanner.Contracts.Admin;
using SwedesEventPlanner.Domain.Bingo;
using SwedesEventPlanner.Domain.Events;
using SwedesEventPlanner.Domain.Players;
using SwedesEventPlanner.Infrastructure.Persistence;

namespace SwedesEventPlanner.Infrastructure.Admin;

public sealed class AdminDevSeedService(
    EventPlannerDbContext dbContext,
    IClock clock) : IAdminDevSeedService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<AdminDevSeedResponse> SeedMockActivityDemoAsync(CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;

        var player = await dbContext.Players
            .SingleOrDefaultAsync(candidate => candidate.RuneScapeName == "Sebbe", cancellationToken);

        if (player is null)
        {
            player = new Player
            {
                DisplayName = "Sebbe",
                RuneScapeName = "Sebbe",
                CreatedAt = now
            };
            dbContext.Players.Add(player);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        var eventDefinition = await dbContext.Events
            .SingleOrDefaultAsync(candidate => candidate.Slug == "local-mock-activity-demo", cancellationToken);

        if (eventDefinition is null)
        {
            eventDefinition = new EventDefinition
            {
                Slug = "local-mock-activity-demo",
                Name = "Local Mock Activity Demo",
                EventType = "bingo",
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
            eventDefinition.Status = EventStatuses.Active;
            eventDefinition.StartsAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
            eventDefinition.EndsAt = new DateTimeOffset(2026, 12, 31, 23, 59, 59, TimeSpan.Zero);
        }

        var team = await dbContext.EventTeams
            .SingleOrDefaultAsync(candidate => candidate.EventId == eventDefinition.Id && candidate.Name == "Blue", cancellationToken);

        if (team is null)
        {
            team = new EventTeam
            {
                EventId = eventDefinition.Id,
                Name = "Blue",
                CreatedAt = now
            };
            dbContext.EventTeams.Add(team);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        var participant = await dbContext.EventParticipants
            .SingleOrDefaultAsync(
                candidate => candidate.EventId == eventDefinition.Id && candidate.PlayerId == player.Id,
                cancellationToken);

        if (participant is null)
        {
            dbContext.EventParticipants.Add(new EventParticipant
            {
                EventId = eventDefinition.Id,
                PlayerId = player.Id,
                TeamId = team.Id,
                JoinedAt = now,
                Status = EventParticipantStatuses.Active
            });
        }
        else
        {
            participant.TeamId = team.Id;
            participant.Status = EventParticipantStatuses.Active;
        }

        var board = await dbContext.BingoBoards
            .SingleOrDefaultAsync(candidate => candidate.EventId == eventDefinition.Id && candidate.Name == "Demo Board", cancellationToken);

        if (board is null)
        {
            board = new BingoBoard
            {
                EventId = eventDefinition.Id,
                Name = "Demo Board",
                CreatedAt = now
            };
            dbContext.BingoBoards.Add(board);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        var pointTile = await EnsureTileAsync(board.Id, "TOB", "Earn Theatre of Blood points.", 1, now, cancellationToken);
        var countTile = await EnsureTileAsync(board.Id, "Pet Drops", "Count pet drops.", 2, now, cancellationToken);

        var pointTier = await EnsureTierAsync(pointTile.Id, 1, "TOB Tier 1", now, cancellationToken);
        var countTier = await EnsureTierAsync(countTile.Id, 1, "Pet Tier 1", now, cancellationToken);

        await EnsureRuleAsync(
            pointTile.Id,
            pointTier.Id,
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

        await EnsureRuleAsync(
            countTile.Id,
            countTier.Id,
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

        await dbContext.SaveChangesAsync(cancellationToken);

        return new AdminDevSeedResponse(
            player.Id,
            eventDefinition.Id,
            team.Id,
            pointTile.Id,
            countTile.Id,
            player.RuneScapeName,
            eventDefinition.Slug,
            [
                """
                {"playerName":"Sebbe","activityType":"item_drop","source":"Theatre of Blood","itemId":22486,"itemName":"Scythe of vitur","quantity":1,"occurredAt":"2026-07-02T18:30:00Z","dedupeKey":"local-demo-scythe-1"}
                """,
                """
                {"playerName":"Sebbe","activityType":"item_drop","source":"Pet Drop","itemId":30000,"itemName":"Tiny demo pet","quantity":1,"occurredAt":"2026-07-02T18:35:00Z","dedupeKey":"local-demo-pet-1"}
                """
            ]);
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
            return tile;
        }

        tile = new BingoTile
        {
            BoardId = boardId,
            Title = title,
            Description = description,
            SortOrder = sortOrder,
            ConfigJson = "{}"
        };
        dbContext.BingoTiles.Add(tile);
        await dbContext.SaveChangesAsync(cancellationToken);
        return tile;
    }

    private async Task<BingoTileTier> EnsureTierAsync(
        long tileId,
        int tierNumber,
        string title,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var tier = await dbContext.BingoTileTiers
            .SingleOrDefaultAsync(candidate => candidate.TileId == tileId && candidate.TierNumber == tierNumber, cancellationToken);

        if (tier is not null)
        {
            return tier;
        }

        tier = new BingoTileTier
        {
            TileId = tileId,
            TierNumber = tierNumber,
            Title = title,
            ScoreValue = 1,
            IsRequiredForBoardCompletion = true,
            ConfigJson = "{}"
        };
        dbContext.BingoTileTiers.Add(tier);
        await dbContext.SaveChangesAsync(cancellationToken);
        return tier;
    }

    private async Task EnsureRuleAsync(
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
            dbContext.TileRules.Add(new TileRule
            {
                TileId = tileId,
                TileTierId = tileTierId,
                RuleType = ruleType,
                Scope = scope,
                IsActive = true,
                ConfigJson = configJson,
                CreatedAt = now
            });
            return;
        }

        rule.Scope = scope;
        rule.IsActive = true;
        rule.ConfigJson = configJson;
    }
}

using Microsoft.EntityFrameworkCore;
using SwedesEventPlanner.Application.Events;
using SwedesEventPlanner.Contracts.Events;
using SwedesEventPlanner.Domain.Bingo;
using SwedesEventPlanner.Domain.Events;
using SwedesEventPlanner.Domain.ExternalCompetitions;
using SwedesEventPlanner.Infrastructure.Bingo;
using SwedesEventPlanner.Infrastructure.Persistence;

namespace SwedesEventPlanner.Infrastructure.Events;

public sealed class EventReadService(EventPlannerDbContext dbContext) : IEventReadService
{
    private static readonly string[] PublicStatuses =
    [
        EventStatuses.Active,
        EventStatuses.Scheduled
    ];

    public async Task<EventListResponse> ListEventsAsync(CancellationToken cancellationToken)
    {
        var events = await dbContext.Events
            .AsNoTracking()
            .Where(eventDefinition => PublicStatuses.Contains(eventDefinition.Status))
            .OrderBy(eventDefinition => eventDefinition.StartsAt)
            .ThenBy(eventDefinition => eventDefinition.Name)
            .Select(eventDefinition => new EventSummaryResponse(
                eventDefinition.Id,
                eventDefinition.Slug,
                eventDefinition.Name,
                eventDefinition.EventType,
                eventDefinition.Status,
                eventDefinition.StartsAt,
                eventDefinition.EndsAt,
                eventDefinition.TimeZone))
            .ToListAsync(cancellationToken);

        return new EventListResponse(events);
    }

    public async Task<EventSummaryResponse?> GetEventAsync(
        string slug,
        CancellationToken cancellationToken)
    {
        return await dbContext.Events
            .AsNoTracking()
            .Where(eventDefinition => eventDefinition.Slug == slug &&
                PublicStatuses.Contains(eventDefinition.Status))
            .Select(eventDefinition => new EventSummaryResponse(
                eventDefinition.Id,
                eventDefinition.Slug,
                eventDefinition.Name,
                eventDefinition.EventType,
                eventDefinition.Status,
                eventDefinition.StartsAt,
                eventDefinition.EndsAt,
                eventDefinition.TimeZone))
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<EventBoardResponse?> GetBoardAsync(
        string slug,
        CancellationToken cancellationToken)
    {
        var eventSummary = await GetEventAsync(slug, cancellationToken);
        if (eventSummary is null)
        {
            return null;
        }

        var board = await dbContext.BingoBoards
            .AsNoTracking()
            .Where(candidate => candidate.EventId == eventSummary.Id)
            .OrderBy(candidate => candidate.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (board is null)
        {
            return null;
        }

        var teams = await GetTeamSummariesAsync(eventSummary.Id, cancellationToken);
        var tiles = await dbContext.BingoTiles
            .AsNoTracking()
            .Where(tile => tile.BoardId == board.Id)
            .OrderBy(tile => tile.SortOrder)
            .ThenBy(tile => tile.Id)
            .ToListAsync(cancellationToken);

        var tileIds = tiles.Select(tile => tile.Id).ToArray();
        var tiers = await dbContext.BingoTileTiers
            .AsNoTracking()
            .Where(tier => tileIds.Contains(tier.TileId))
            .OrderBy(tier => tier.SortOrder)
            .ThenBy(tier => tier.TierNumber)
            .ToListAsync(cancellationToken);

        var rules = await dbContext.TileRules
            .AsNoTracking()
            .Where(rule => tileIds.Contains(rule.TileId) && rule.IsActive)
            .ToListAsync(cancellationToken);

        var tileProgress = await dbContext.EventTileProgress
            .AsNoTracking()
            .Where(progress => progress.EventId == eventSummary.Id && tileIds.Contains(progress.TileId))
            .ToListAsync(cancellationToken);

        var tierProgress = await dbContext.EventTileTierProgress
            .AsNoTracking()
            .Where(progress => progress.EventId == eventSummary.Id && tileIds.Contains(progress.TileId))
            .ToListAsync(cancellationToken);

        var externalCompetitionFreshness = await dbContext.ExternalCompetitions
            .AsNoTracking()
            .Where(competition => competition.EventId == eventSummary.Id &&
                competition.Status == ExternalCompetitionStatuses.Active)
            .OrderBy(competition => competition.Name)
            .Select(competition => new EventExternalCompetitionFreshnessResponse(
                competition.Id,
                competition.Provider,
                competition.Name,
                competition.MetricType,
                competition.MetricKey,
                competition.LastSuccessfulSyncAt,
                competition.LastSyncStatus,
                competition.NextPublicSyncAvailableAt))
            .ToListAsync(cancellationToken);

        var boardTiles = tiles
            .Select(tile =>
            {
                var tileTeamProgress = teams
                    .Select(team =>
                    {
                        var progress = tileProgress.SingleOrDefault(candidate =>
                            candidate.TileId == tile.Id &&
                            candidate.TeamId == team.Id &&
                            candidate.PlayerId == null);

                        return new BoardTileTeamProgressResponse(
                            team.Id,
                            team.Name,
                            progress?.CurrentValue ?? 0,
                            progress?.CurrentTier ?? 0,
                            progress?.IsCompleted ?? false,
                            progress?.CompletedAt);
                    })
                    .ToList();

                var tileTiers = tiers
                    .Where(tier => tier.TileId == tile.Id)
                    .Select(tier =>
                    {
                        var requiredValue = GetRequiredValueForTier(tier, rules);
                        var tierTeamProgress = teams
                            .Select(team =>
                            {
                                var progress = tierProgress.SingleOrDefault(candidate =>
                                    candidate.TileTierId == tier.Id &&
                                    candidate.TeamId == team.Id &&
                                    candidate.PlayerId == null);

                                return new BoardTileTierTeamProgressResponse(
                                    team.Id,
                                    team.Name,
                                    progress?.CurrentValue ?? 0,
                                    progress?.IsAchieved ?? false,
                                    progress?.AchievedAt,
                                    progress?.IsScored ?? false,
                                    progress?.ScoredAt,
                                    progress?.ScoreAwarded ?? 0);
                            })
                            .ToList();

                        return new BoardTileTierResponse(
                            tier.Id,
                            tier.TierNumber,
                            tier.Title,
                            tier.Description,
                            tier.ScoreValue,
                            tier.IsRequiredForBoardCompletion,
                            requiredValue,
                            tierTeamProgress);
                    })
                    .ToList();

                return new BoardTileResponse(
                    tile.Id,
                    tile.Title,
                    tile.Description,
                    tile.PositionX,
                    tile.PositionY,
                    tile.SortOrder,
                    tileTeamProgress,
                    tileTiers);
            })
            .ToList();

        return new EventBoardResponse(
            eventSummary,
            new BoardResponse(
                board.Id,
                board.Name,
                board.Rows,
                board.Columns,
                boardTiles),
            teams
                .Select(team => new EventBoardTeamResponse(
                    team.Id,
                    team.Name,
                    team.Score,
                    team.ScoredTiers,
                    team.CompletedTiles,
                    team.CurrentValue))
                .ToList(),
            externalCompetitionFreshness);
    }

    public async Task<EventTeamListResponse?> GetTeamsAsync(
        string slug,
        CancellationToken cancellationToken)
    {
        var eventSummary = await GetEventAsync(slug, cancellationToken);
        if (eventSummary is null)
        {
            return null;
        }

        var teams = await GetTeamSummariesAsync(eventSummary.Id, cancellationToken);

        return new EventTeamListResponse(eventSummary, teams);
    }

    public async Task<EventTeamBoardResponse?> GetTeamBoardAsync(
        string slug,
        long teamId,
        CancellationToken cancellationToken)
    {
        var board = await GetBoardAsync(slug, cancellationToken);
        if (board is null)
        {
            return null;
        }

        var team = board.Teams.SingleOrDefault(candidate => candidate.Id == teamId);
        if (team is null)
        {
            return null;
        }

        var teamTiles = board.Board.Tiles
            .Select(tile => new BoardTileResponse(
                tile.Id,
                tile.Title,
                tile.Description,
                tile.PositionX,
                tile.PositionY,
                tile.SortOrder,
                tile.TeamProgress.Where(progress => progress.TeamId == teamId).ToList(),
                tile.Tiers
                    .Select(tier => new BoardTileTierResponse(
                        tier.Id,
                        tier.TierNumber,
                        tier.Title,
                        tier.Description,
                        tier.ScoreValue,
                        tier.IsRequiredForBoardCompletion,
                        tier.RequiredValue,
                        tier.TeamProgress.Where(progress => progress.TeamId == teamId).ToList()))
                    .ToList()))
            .ToList();

        return new EventTeamBoardResponse(
            board.Event,
            team,
            new BoardResponse(
                board.Board.Id,
                board.Board.Name,
                board.Board.Rows,
                board.Board.Columns,
                teamTiles),
            board.ExternalCompetitionFreshness);
    }

    public async Task<EventContributionListResponse?> GetContributionsAsync(
        string slug,
        int limit,
        CancellationToken cancellationToken)
    {
        var eventSummary = await GetEventAsync(slug, cancellationToken);
        if (eventSummary is null)
        {
            return null;
        }

        var boundedLimit = Math.Clamp(limit, 1, 100);
        var contributions = await (
                from contribution in dbContext.EventProgressContributions.AsNoTracking()
                join player in dbContext.Players.AsNoTracking() on contribution.PlayerId equals player.Id into playerJoin
                from player in playerJoin.DefaultIfEmpty()
                join tile in dbContext.BingoTiles.AsNoTracking() on contribution.TileId equals tile.Id
                join tier in dbContext.BingoTileTiers.AsNoTracking() on contribution.TileTierId equals tier.Id into tierJoin
                from tier in tierJoin.DefaultIfEmpty()
                join team in dbContext.EventTeams.AsNoTracking() on contribution.TeamId equals team.Id into teamJoin
                from team in teamJoin.DefaultIfEmpty()
                where contribution.EventId == eventSummary.Id && contribution.ValueAdded > 0
                orderby contribution.CreatedAt descending, contribution.Id descending
                select new EventContributionResponse(
                    contribution.Id,
                    player == null ? "TempleOSRS sync" : player.DisplayName,
                    contribution.TeamId,
                    team == null ? null : team.Name,
                    tile.Title,
                    tier == null ? null : tier.Title,
                    contribution.ValueAdded,
                    contribution.Description,
                    contribution.CreatedAt))
            .Take(boundedLimit)
            .ToListAsync(cancellationToken);

        return new EventContributionListResponse(eventSummary, contributions);
    }

    private async Task<List<EventTeamSummaryResponse>> GetTeamSummariesAsync(
        long eventId,
        CancellationToken cancellationToken)
    {
        var teams = await dbContext.EventTeams
            .AsNoTracking()
            .Where(team => team.EventId == eventId)
            .OrderBy(team => team.Name)
            .ToListAsync(cancellationToken);

        var teamIds = teams.Select(team => team.Id).ToArray();
        var tierProgress = await dbContext.EventTileTierProgress
            .AsNoTracking()
            .Where(progress => progress.EventId == eventId &&
                progress.TeamId.HasValue &&
                teamIds.Contains(progress.TeamId.Value))
            .ToListAsync(cancellationToken);

        var tileProgress = await dbContext.EventTileProgress
            .AsNoTracking()
            .Where(progress => progress.EventId == eventId &&
                progress.TeamId.HasValue &&
                teamIds.Contains(progress.TeamId.Value))
            .ToListAsync(cancellationToken);

        var contributionCounts = await dbContext.EventProgressContributions
            .AsNoTracking()
            .Where(contribution => contribution.EventId == eventId &&
                contribution.TeamId.HasValue &&
                teamIds.Contains(contribution.TeamId.Value) &&
                contribution.ValueAdded > 0)
            .GroupBy(contribution => contribution.TeamId!.Value)
            .Select(group => new
            {
                TeamId = group.Key,
                Count = group.Count()
            })
            .ToListAsync(cancellationToken);

        return teams
            .Select(team =>
            {
                var teamTierProgress = tierProgress
                    .Where(progress => progress.TeamId == team.Id)
                    .ToList();
                var teamTileProgress = tileProgress
                    .Where(progress => progress.TeamId == team.Id)
                    .ToList();

                return new EventTeamSummaryResponse(
                    team.Id,
                    team.Name,
                    teamTierProgress.Sum(progress => progress.ScoreAwarded),
                    teamTierProgress.Count(progress => progress.IsScored),
                    teamTileProgress.Count(progress => progress.IsCompleted),
                    teamTileProgress.Sum(progress => progress.CurrentValue),
                    contributionCounts.SingleOrDefault(candidate => candidate.TeamId == team.Id)?.Count ?? 0);
            })
            .OrderByDescending(team => team.Score)
            .ThenByDescending(team => team.CurrentValue)
            .ThenBy(team => team.Name)
            .ToList();
    }

    private static decimal? GetRequiredValueForTier(
        BingoTileTier tier,
        IReadOnlyCollection<TileRule> rules)
    {
        var rule = rules
            .Where(candidate => candidate.TileTierId == tier.Id)
            .OrderBy(candidate => candidate.Id)
            .FirstOrDefault();

        if (rule is null)
        {
            return null;
        }

        return TierProgressScoringService.GetRequiredValue(rule.ConfigJson, tier.TierNumber);
    }
}

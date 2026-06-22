using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SwedesEventPlanner.Domain.Bingo;
using SwedesEventPlanner.Infrastructure.Persistence;

namespace SwedesEventPlanner.Infrastructure.Bingo;

internal sealed class TierProgressScoringService(EventPlannerDbContext dbContext)
{
    public async Task RecalculateTileScopeAsync(
        long eventId,
        long tileId,
        long? teamId,
        long? playerId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var tiers = await dbContext.BingoTileTiers
            .Where(tier => tier.TileId == tileId)
            .OrderBy(tier => tier.SortOrder)
            .ThenBy(tier => tier.TierNumber)
            .ThenBy(tier => tier.Id)
            .ToListAsync(cancellationToken);

        if (tiers.Count == 0)
        {
            return;
        }

        var tierIds = tiers.Select(tier => tier.Id).ToArray();
        var rules = await dbContext.TileRules
            .Where(rule => rule.TileId == tileId &&
                rule.TileTierId.HasValue &&
                tierIds.Contains(rule.TileTierId.Value) &&
                rule.IsActive)
            .OrderBy(rule => rule.Id)
            .ToListAsync(cancellationToken);

        var progressRows = await dbContext.EventTileTierProgress
            .Where(progress => progress.EventId == eventId &&
                progress.TileId == tileId &&
                progress.TeamId == teamId &&
                progress.PlayerId == playerId)
            .ToListAsync(cancellationToken);

        var earlierRequiredTiersScored = true;
        var currentScoredTier = 0;
        var requiredTierIds = new List<long>();
        var scoredRequiredTierIds = new HashSet<long>();

        foreach (var tier in tiers)
        {
            if (tier.IsRequiredForBoardCompletion)
            {
                requiredTierIds.Add(tier.Id);
            }

            var tierProgress = progressRows.SingleOrDefault(progress => progress.TileTierId == tier.Id);
            if (tierProgress is null)
            {
                if (tier.IsRequiredForBoardCompletion)
                {
                    earlierRequiredTiersScored = false;
                }

                continue;
            }

            var requiredValue = GetRequiredValue(tier, rules);
            var achieved = requiredValue.HasValue && tierProgress.CurrentValue >= requiredValue.Value;
            tierProgress.IsAchieved = achieved;
            tierProgress.AchievedAt = achieved ? tierProgress.AchievedAt ?? now : null;

            var canScore = achieved && earlierRequiredTiersScored;
            tierProgress.IsScored = canScore;
            tierProgress.ScoredAt = canScore ? tierProgress.ScoredAt ?? now : null;
            tierProgress.ScoreAwarded = canScore ? tier.ScoreValue : 0;
            tierProgress.UpdatedAt = now;

            if (canScore)
            {
                currentScoredTier = Math.Max(currentScoredTier, tier.TierNumber);
                if (tier.IsRequiredForBoardCompletion)
                {
                    scoredRequiredTierIds.Add(tier.Id);
                }
            }

            if (tier.IsRequiredForBoardCompletion)
            {
                earlierRequiredTiersScored = canScore;
            }
        }

        var tileProgress = await dbContext.EventTileProgress.SingleOrDefaultAsync(
            progress => progress.EventId == eventId &&
                progress.TileId == tileId &&
                progress.TeamId == teamId &&
                progress.PlayerId == playerId,
            cancellationToken);

        if (tileProgress is null)
        {
            return;
        }

        tileProgress.CurrentTier = currentScoredTier;
        tileProgress.IsCompleted = requiredTierIds.Count > 0 && requiredTierIds.All(scoredRequiredTierIds.Contains);
        tileProgress.CompletedAt = tileProgress.IsCompleted ? tileProgress.CompletedAt ?? now : null;
        tileProgress.UpdatedAt = now;
    }

    public static decimal? GetRequiredValue(BingoTileTier tier, IReadOnlyCollection<TileRule> rules)
    {
        var rule = rules
            .Where(candidate => candidate.TileTierId == tier.Id)
            .OrderBy(candidate => candidate.Id)
            .FirstOrDefault();

        return rule is null ? null : GetRequiredValue(rule.ConfigJson, tier.TierNumber);
    }

    public static decimal? GetRequiredValue(string configJson, int tierNumber)
    {
        using var config = JsonDocument.Parse(string.IsNullOrWhiteSpace(configJson) ? "{}" : configJson);

        if (config.RootElement.TryGetProperty("required", out var requiredElement) &&
            requiredElement.ValueKind == JsonValueKind.Number)
        {
            return requiredElement.GetDecimal();
        }

        if (!config.RootElement.TryGetProperty("tiers", out var tiersElement) ||
            tiersElement.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        foreach (var tierElement in tiersElement.EnumerateArray())
        {
            if (tierElement.TryGetProperty("tier", out var tierNumberElement) &&
                tierNumberElement.GetInt32() == tierNumber &&
                tierElement.TryGetProperty("required", out var tierRequiredElement) &&
                tierRequiredElement.ValueKind == JsonValueKind.Number)
            {
                return tierRequiredElement.GetDecimal();
            }
        }

        return null;
    }
}

using System.Text.Json;
using System.Text.Json.Nodes;
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

        tileProgress.CurrentValue = GetDerivedTileCurrentValue(tiers, rules, progressRows);
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

        if (config.RootElement.TryGetProperty("requiredValue", out var requiredValueElement) &&
            requiredValueElement.ValueKind == JsonValueKind.Number)
        {
            return requiredValueElement.GetDecimal();
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

            if (tierElement.TryGetProperty("tier", out var tierValueNumberElement) &&
                tierValueNumberElement.GetInt32() == tierNumber &&
                tierElement.TryGetProperty("requiredValue", out var tierRequiredValueElement) &&
                tierRequiredValueElement.ValueKind == JsonValueKind.Number)
            {
                return tierRequiredValueElement.GetDecimal();
            }
        }

        return null;
    }

    private static decimal GetDerivedTileCurrentValue(
        IReadOnlyList<BingoTileTier> tiers,
        IReadOnlyCollection<TileRule> rules,
        IReadOnlyCollection<EventTileTierProgress> progressRows)
    {
        if (progressRows.Count == 0)
        {
            return 0;
        }

        if (UsesSingleProgressUnit(tiers, rules))
        {
            return progressRows.Max(progress => progress.CurrentValue);
        }

        return progressRows.Sum(progress => progress.ScoreAwarded);
    }

    private static bool UsesSingleProgressUnit(
        IReadOnlyList<BingoTileTier> tiers,
        IReadOnlyCollection<TileRule> rules)
    {
        var unitKeys = new HashSet<string>(StringComparer.Ordinal);

        foreach (var tier in tiers)
        {
            var rule = rules
                .Where(candidate => candidate.TileTierId == tier.Id)
                .OrderBy(candidate => candidate.Id)
                .FirstOrDefault();

            if (rule is null)
            {
                continue;
            }

            unitKeys.Add($"{rule.RuleType}:{NormalizeRuleUnitConfig(rule.ConfigJson)}");
            if (unitKeys.Count > 1)
            {
                return false;
            }
        }

        return true;
    }

    private static string NormalizeRuleUnitConfig(string configJson)
    {
        var node = JsonNode.Parse(string.IsNullOrWhiteSpace(configJson) ? "{}" : configJson);
        if (node is not JsonObject root)
        {
            return "{}";
        }

        RemoveThresholdProperties(root);
        return root.ToJsonString(new JsonSerializerOptions { WriteIndented = false });
    }

    private static void RemoveThresholdProperties(JsonObject obj)
    {
        obj.Remove("required");
        obj.Remove("requiredValue");
        obj.Remove("tiers");
    }
}

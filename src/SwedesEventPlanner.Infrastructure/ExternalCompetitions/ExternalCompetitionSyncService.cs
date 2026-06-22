using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SwedesEventPlanner.Application.Admin;
using SwedesEventPlanner.Application.Clock;
using SwedesEventPlanner.Application.ExternalCompetitions;
using SwedesEventPlanner.Contracts.Admin;
using SwedesEventPlanner.Contracts.Events;
using SwedesEventPlanner.Domain.Bingo;
using SwedesEventPlanner.Domain.Common;
using SwedesEventPlanner.Domain.Events;
using SwedesEventPlanner.Domain.ExternalCompetitions;
using SwedesEventPlanner.Domain.Players;
using SwedesEventPlanner.Infrastructure.Bingo;
using SwedesEventPlanner.Infrastructure.Persistence;

namespace SwedesEventPlanner.Infrastructure.ExternalCompetitions;

public sealed class ExternalCompetitionSyncService(
    EventPlannerDbContext dbContext,
    ITempleOsrsClient templeClient,
    IClock clock,
    IOptions<TempleOsrsOptions> options,
    ILogger<ExternalCompetitionSyncService> logger) : IExternalCompetitionSyncService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly ConcurrentDictionary<long, SemaphoreSlim> SyncLocks = new();

    private TimeSpan PublicRefreshCooldown =>
        TimeSpan.FromMinutes(Math.Max(0, options.Value.PublicRefreshCooldownMinutes));

    private TimeSpan AutoSyncInterval =>
        TimeSpan.FromMinutes(Math.Max(1, options.Value.AutoSyncIntervalMinutes));

    public async Task<AdminExternalCompetitionResponse?> LinkTempleCompetitionAsync(
        string eventSlug,
        LinkExternalCompetitionRequest request,
        CancellationToken cancellationToken)
    {
        var eventDefinition = await FindEventAsync(eventSlug, cancellationToken);
        if (eventDefinition is null)
        {
            return null;
        }

        var externalId = request.ExternalId.Trim();
        if (string.IsNullOrWhiteSpace(externalId))
        {
            throw new AdminEventSetupException("Temple competition ID is required.");
        }

        var competition = await dbContext.ExternalCompetitions.SingleOrDefaultAsync(
            candidate => candidate.Provider == ExternalCompetitionProviders.TempleOsrs &&
                candidate.ExternalId == externalId &&
                candidate.EventId == eventDefinition.Id,
            cancellationToken);

        if (competition is null)
        {
            competition = new ExternalCompetition
            {
                EventId = eventDefinition.Id,
                Provider = ExternalCompetitionProviders.TempleOsrs,
                ExternalId = externalId,
                Name = string.IsNullOrWhiteSpace(request.Name) ? $"Temple {externalId}" : request.Name.Trim(),
                MetricType = NormalizeMetric(request.MetricType, "xp"),
                MetricKey = NormalizeMetric(request.MetricKey, "overall"),
                CompetitionMode = ExternalCompetitionModes.Unknown,
                Status = ExternalCompetitionStatuses.Active,
                CreatedAt = clock.UtcNow,
                ConfigJson = JsonSerializer.Serialize(new { source = "admin_link" }, JsonOptions)
            };
            dbContext.ExternalCompetitions.Add(competition);
        }
        else
        {
            competition.Name = string.IsNullOrWhiteSpace(request.Name) ? competition.Name : request.Name.Trim();
            competition.MetricType = NormalizeMetric(request.MetricType, competition.MetricType);
            competition.MetricKey = NormalizeMetric(request.MetricKey, competition.MetricKey);
            competition.Status = ExternalCompetitionStatuses.Active;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return MapCompetition(competition);
    }

    public async Task<AdminExternalCompetitionListResponse?> ListCompetitionsAsync(
        string eventSlug,
        CancellationToken cancellationToken)
    {
        var eventDefinition = await FindEventAsync(eventSlug, cancellationToken);
        if (eventDefinition is null)
        {
            return null;
        }

        var competitions = await dbContext.ExternalCompetitions
            .AsNoTracking()
            .Where(competition => competition.EventId == eventDefinition.Id)
            .OrderBy(competition => competition.Name)
            .Select(competition => MapCompetition(competition))
            .ToListAsync(cancellationToken);

        return new AdminExternalCompetitionListResponse(MapEvent(eventDefinition), competitions);
    }

    public async Task<AdminExternalCompetitionSyncRunResponse?> SyncCompetitionAsync(
        string eventSlug,
        long externalCompetitionId,
        CancellationToken cancellationToken)
    {
        return await SyncCompetitionAsync(
            eventSlug,
            externalCompetitionId,
            triggerType: "admin",
            cancellationToken);
    }

    public async Task<EventTempleRefreshResponse?> RequestPublicRefreshAsync(
        string eventSlug,
        CancellationToken cancellationToken)
    {
        var eventDefinition = await FindEventAsync(eventSlug, cancellationToken);
        if (eventDefinition is null)
        {
            return null;
        }

        var competitions = await dbContext.ExternalCompetitions
            .Where(competition => competition.EventId == eventDefinition.Id &&
                competition.Provider == ExternalCompetitionProviders.TempleOsrs &&
                competition.Status == ExternalCompetitionStatuses.Active)
            .OrderBy(competition => competition.Name)
            .ToListAsync(cancellationToken);

        var results = new List<EventTempleRefreshCompetitionResponse>();
        foreach (var competition in competitions)
        {
            var now = clock.UtcNow;
            var nextAvailableAt = competition.NextPublicSyncAvailableAt ??
                competition.LastPublicSyncRequestAcceptedAt?.Add(PublicRefreshCooldown);

            if (nextAvailableAt.HasValue && nextAvailableAt.Value > now)
            {
                results.Add(MapRefreshCompetition(
                    competition,
                    refreshRequested: false,
                    status: competition.LastSyncStatus ?? "cooldown",
                    message: $"Refresh available {FormatCooldown(nextAvailableAt.Value - now)}.",
                    nextAvailableAt));
                continue;
            }

            var hasActiveRun = await HasActiveSyncRunAsync(competition.Id, cancellationToken);
            if (hasActiveRun)
            {
                results.Add(MapRefreshCompetition(
                    competition,
                    refreshRequested: false,
                    status: ExternalCompetitionSyncRunStatuses.SkippedAlreadyRunning,
                    message: "A TempleOSRS refresh is already running.",
                    nextAvailableAt));
                continue;
            }

            competition.LastPublicSyncRequestAcceptedAt = now;
            competition.NextPublicSyncAvailableAt = now.Add(PublicRefreshCooldown);
            await dbContext.SaveChangesAsync(cancellationToken);

            var run = await SyncCompetitionAsync(
                eventSlug,
                competition.Id,
                triggerType: "public_refresh",
                cancellationToken);

            await dbContext.Entry(competition).ReloadAsync(cancellationToken);
            results.Add(MapRefreshCompetition(
                competition,
                refreshRequested: run is not null &&
                    run.Status != ExternalCompetitionSyncRunStatuses.SkippedAlreadyRunning,
                status: run?.Status ?? competition.LastSyncStatus ?? "unknown",
                message: GetPublicRefreshMessage(run),
                competition.NextPublicSyncAvailableAt));
        }

        if (results.Count == 0)
        {
            results.Add(new EventTempleRefreshCompetitionResponse(
                0,
                "TempleOSRS",
                string.Empty,
                "not_configured",
                RefreshRequested: false,
                LastSuccessfulSyncAt: null,
                NextRefreshAvailableAt: null,
                "No linked TempleOSRS competitions are available for this event."));
        }

        return new EventTempleRefreshResponse(MapEventSummary(eventDefinition), results);
    }

    public async Task<int> SyncDueActiveCompetitionsAsync(CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var dueBefore = now.Subtract(AutoSyncInterval);
        var dueCompetitions = await (
                from competition in dbContext.ExternalCompetitions.AsNoTracking()
                join eventDefinition in dbContext.Events.AsNoTracking()
                    on competition.EventId equals eventDefinition.Id
                where competition.Provider == ExternalCompetitionProviders.TempleOsrs &&
                    competition.Status == ExternalCompetitionStatuses.Active &&
                    eventDefinition.Status == EventStatuses.Active &&
                    eventDefinition.StartsAt <= now &&
                    (eventDefinition.EndsAt == null || eventDefinition.EndsAt >= now) &&
                    (competition.LastSyncedAt == null || competition.LastSyncedAt <= dueBefore)
                orderby competition.LastSyncedAt ?? DateTimeOffset.MinValue, competition.Id
                select new
                {
                    competition.Id,
                    eventDefinition.Slug
                })
            .ToListAsync(cancellationToken);

        var started = 0;
        foreach (var competition in dueCompetitions)
        {
            var run = await SyncCompetitionAsync(
                competition.Slug,
                competition.Id,
                triggerType: "worker_periodic",
                cancellationToken);

            if (run is not null &&
                run.Status != ExternalCompetitionSyncRunStatuses.SkippedAlreadyRunning &&
                run.Status != ExternalCompetitionSyncRunStatuses.SkippedCooldown)
            {
                started++;
            }
        }

        return started;
    }

    private async Task<AdminExternalCompetitionSyncRunResponse?> SyncCompetitionAsync(
        string eventSlug,
        long externalCompetitionId,
        string triggerType,
        CancellationToken cancellationToken)
    {
        var eventDefinition = await FindEventAsync(eventSlug, cancellationToken);
        if (eventDefinition is null)
        {
            return null;
        }

        var competition = await dbContext.ExternalCompetitions.SingleOrDefaultAsync(
            candidate => candidate.Id == externalCompetitionId && candidate.EventId == eventDefinition.Id,
            cancellationToken);

        if (competition is null)
        {
            return null;
        }

        var gate = SyncLocks.GetOrAdd(competition.Id, _ => new SemaphoreSlim(1, 1));
        if (!await gate.WaitAsync(0, cancellationToken))
        {
            var skipped = await CreateSkippedRunAsync(competition.Id, triggerType, cancellationToken);
            return MapRun(skipped);
        }

        ExternalCompetitionSyncRun run;
        try
        {
            var hasActiveRun = await HasActiveSyncRunAsync(competition.Id, cancellationToken);

            if (hasActiveRun)
            {
                var skipped = await CreateSkippedRunAsync(competition.Id, triggerType, cancellationToken);
                return MapRun(skipped);
            }

            run = new ExternalCompetitionSyncRun
            {
                ExternalCompetitionId = competition.Id,
                TriggerType = triggerType,
                RequestedAt = clock.UtcNow,
                StartedAt = clock.UtcNow,
                Status = ExternalCompetitionSyncRunStatuses.Running,
                MetadataJson = JsonSerializer.Serialize(new { provider = competition.Provider }, JsonOptions)
            };
            dbContext.ExternalCompetitionSyncRuns.Add(run);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        finally
        {
            gate.Release();
        }

        try
        {
            var info = await templeClient.GetCompetitionInfoAsync(competition.ExternalId, cancellationToken);
            competition.Name = string.IsNullOrWhiteSpace(info.Name) ? competition.Name : info.Name;
            competition.CompetitionMode = info.IsTeamCompetition
                ? ExternalCompetitionModes.Team
                : ExternalCompetitionModes.Individual;
            competition.MetricKey = string.IsNullOrWhiteSpace(competition.MetricKey)
                ? info.MetricKey
                : competition.MetricKey;

            var rowsChanged = await CacheTempleResultAsync(competition, info, cancellationToken);
            var progressRowsChanged = await RecalculateExternalMetricRulesAsync(competition, run.Id, cancellationToken);
            var completedAt = clock.UtcNow;

            competition.LastSyncedAt = completedAt;
            competition.LastSuccessfulSyncAt = completedAt;
            competition.LastSyncStatus = ExternalCompetitionSyncRunStatuses.Succeeded;
            competition.LastSyncError = null;

            run.CompletedAt = completedAt;
            run.Status = ExternalCompetitionSyncRunStatuses.Succeeded;
            run.RowsRead = info.Participants.Count + info.Teams.Count;
            run.RowsChanged = rowsChanged + progressRowsChanged;
            run.RawResponseJson = null;

            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(exception, "TempleOSRS read-only sync failed for external competition {ExternalCompetitionId}.", competition.Id);

            var completedAt = clock.UtcNow;
            competition.LastSyncedAt = completedAt;
            competition.LastSyncStatus = ExternalCompetitionSyncRunStatuses.Failed;
            competition.LastSyncError = SafeError(exception);
            run.CompletedAt = completedAt;
            run.Status = ExternalCompetitionSyncRunStatuses.Failed;
            run.ErrorMessage = SafeError(exception);

            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return MapRun(run);
    }

    public async Task<AdminExternalCompetitionSyncRunListResponse> ListSyncRunsAsync(
        long externalCompetitionId,
        CancellationToken cancellationToken)
    {
        var runs = await dbContext.ExternalCompetitionSyncRuns
            .AsNoTracking()
            .Where(run => run.ExternalCompetitionId == externalCompetitionId)
            .OrderByDescending(run => run.StartedAt)
            .ThenByDescending(run => run.Id)
            .Take(20)
            .Select(run => MapRun(run))
            .ToListAsync(cancellationToken);

        return new AdminExternalCompetitionSyncRunListResponse(runs);
    }

    public async Task<AdminExternalCompetitionPlayerMetricListResponse> ListPlayerMetricsAsync(
        long externalCompetitionId,
        CancellationToken cancellationToken)
    {
        var metrics = await (
                from metric in dbContext.ExternalCompetitionMetrics.AsNoTracking()
                join player in dbContext.Players.AsNoTracking() on metric.PlayerId equals player.Id into playerJoin
                from player in playerJoin.DefaultIfEmpty()
                where metric.ExternalCompetitionId == externalCompetitionId
                orderby metric.Rank, metric.RuneScapeName
                select new AdminExternalCompetitionPlayerMetricResponse(
                    metric.Id,
                    metric.RuneScapeName,
                    metric.PlayerId,
                    player == null ? null : player.DisplayName,
                    metric.MetricType,
                    metric.MetricKey,
                    metric.StartValue,
                    metric.CurrentValue,
                    metric.GainedValue,
                    metric.Rank,
                    metric.LastSyncedAt))
            .ToListAsync(cancellationToken);

        return new AdminExternalCompetitionPlayerMetricListResponse(metrics);
    }

    public async Task<AdminExternalCompetitionTeamMetricListResponse> ListTeamMetricsAsync(
        long externalCompetitionId,
        CancellationToken cancellationToken)
    {
        var metrics = await (
                from metric in dbContext.ExternalCompetitionTeamMetrics.AsNoTracking()
                join team in dbContext.EventTeams.AsNoTracking() on metric.LocalTeamId equals team.Id into teamJoin
                from team in teamJoin.DefaultIfEmpty()
                where metric.ExternalCompetitionId == externalCompetitionId
                orderby metric.Rank, metric.TeamName
                select new
                {
                    Metric = metric,
                    LocalTeamName = team == null ? null : team.Name
                })
            .ToListAsync(cancellationToken);

        return new AdminExternalCompetitionTeamMetricListResponse(metrics
            .Select(row => new AdminExternalCompetitionTeamMetricResponse(
                row.Metric.Id,
                row.Metric.TempleTeamKey,
                row.Metric.TeamName,
                row.Metric.LocalTeamId,
                row.LocalTeamName,
                row.Metric.MetricType,
                row.Metric.MetricKey,
                row.Metric.StartValue,
                row.Metric.CurrentValue,
                row.Metric.GainedValue,
                row.Metric.Rank,
                row.Metric.MvpRuneScapeName,
                DeserializeMembers(row.Metric.MembersJson),
                row.Metric.LastSyncedAt,
                row.Metric.LocalTeamId is null ||
                    !string.Equals(row.Metric.TeamName, row.LocalTeamName, StringComparison.OrdinalIgnoreCase)))
            .ToList());
    }

    public async Task<AdminExternalCompetitionUnmatchedIdentityListResponse> ListUnmatchedIdentitiesAsync(
        long externalCompetitionId,
        CancellationToken cancellationToken)
    {
        var identities = await (
                from metric in dbContext.ExternalCompetitionMetrics.AsNoTracking()
                join identity in dbContext.ExternalPlayerIdentities.AsNoTracking()
                    on metric.ExternalPlayerIdentityId equals identity.Id
                where metric.ExternalCompetitionId == externalCompetitionId &&
                    metric.PlayerId == null &&
                    identity.Status == ExternalPlayerIdentityStatuses.Unmatched
                orderby identity.DisplayName
                select new AdminExternalCompetitionUnmatchedIdentityResponse(
                    identity.Id,
                    identity.ExternalIdentifier,
                    identity.DisplayName,
                    identity.FirstSeenAt,
                    identity.LastSeenAt))
            .Distinct()
            .ToListAsync(cancellationToken);

        return new AdminExternalCompetitionUnmatchedIdentityListResponse(identities);
    }

    private async Task<int> CacheTempleResultAsync(
        ExternalCompetition competition,
        TempleOsrsCompetitionInfo info,
        CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var rowsChanged = 0;
        var eventTeams = await dbContext.EventTeams
            .Where(team => team.EventId == competition.EventId)
            .ToListAsync(cancellationToken);

        var existingMetrics = await dbContext.ExternalCompetitionMetrics
            .Where(metric => metric.ExternalCompetitionId == competition.Id)
            .ToListAsync(cancellationToken);
        dbContext.ExternalCompetitionMetrics.RemoveRange(existingMetrics);

        var existingTeamMetrics = await dbContext.ExternalCompetitionTeamMetrics
            .Where(metric => metric.ExternalCompetitionId == competition.Id)
            .ToListAsync(cancellationToken);
        dbContext.ExternalCompetitionTeamMetrics.RemoveRange(existingTeamMetrics);
        rowsChanged += existingMetrics.Count + existingTeamMetrics.Count;

        foreach (var participant in info.Participants)
        {
            var normalizedName = NormalizeIdentity(participant.RuneScapeName);
            var playerId = await FindPlayerIdAsync(normalizedName, cancellationToken);
            var identity = await EnsureIdentityAsync(normalizedName, participant.DisplayName, playerId, now, cancellationToken);
            var review = await EnsureReviewAsync(competition.Id, identity.Id, playerId, cancellationToken);

            dbContext.ExternalCompetitionMetrics.Add(new ExternalCompetitionMetric
            {
                ExternalCompetitionId = competition.Id,
                ExternalPlayerIdentityId = identity.Id,
                ExternalCompetitionPlayerReviewId = review.Id,
                PlayerId = playerId,
                RuneScapeName = participant.RuneScapeName,
                MetricType = competition.MetricType,
                MetricKey = competition.MetricKey,
                StartValue = participant.StartValue,
                CurrentValue = participant.CurrentValue,
                GainedValue = participant.GainedValue,
                Rank = participant.Rank,
                LastSyncedAt = now,
                MetadataJson = JsonSerializer.Serialize(new
                {
                    participant.TeamKey,
                    participant.TeamName,
                    participant.LastCheckedAt,
                    participant.LastChangedAt,
                    participant.HasDatapoints,
                    participant.OnHiscores,
                    displayName = participant.DisplayName
                }, JsonOptions)
            });
            rowsChanged++;
        }

        foreach (var teamMetric in info.Teams)
        {
            var localTeam = eventTeams.FirstOrDefault(team =>
                string.Equals(team.Name, teamMetric.TeamName, StringComparison.OrdinalIgnoreCase));

            dbContext.ExternalCompetitionTeamMetrics.Add(new ExternalCompetitionTeamMetric
            {
                ExternalCompetitionId = competition.Id,
                LocalTeamId = localTeam?.Id,
                TempleTeamKey = teamMetric.TempleTeamKey,
                TeamName = teamMetric.TeamName,
                MetricType = competition.MetricType,
                MetricKey = competition.MetricKey,
                StartValue = teamMetric.StartValue,
                CurrentValue = teamMetric.CurrentValue,
                GainedValue = teamMetric.GainedValue,
                Rank = teamMetric.Rank,
                MvpRuneScapeName = teamMetric.MvpRuneScapeName,
                MembersJson = JsonSerializer.Serialize(teamMetric.Members, JsonOptions),
                LastSyncedAt = now,
                MetadataJson = JsonSerializer.Serialize(new { source = "templeosrs" }, JsonOptions)
            });
            rowsChanged++;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return rowsChanged;
    }

    private async Task<int> RecalculateExternalMetricRulesAsync(
        ExternalCompetition competition,
        long syncRunId,
        CancellationToken cancellationToken)
    {
        var boardIds = await dbContext.BingoBoards
            .Where(board => board.EventId == competition.EventId)
            .Select(board => board.Id)
            .ToListAsync(cancellationToken);
        var tileIds = await dbContext.BingoTiles
            .Where(tile => boardIds.Contains(tile.BoardId))
            .Select(tile => tile.Id)
            .ToListAsync(cancellationToken);
        var rules = await dbContext.TileRules
            .Where(rule => tileIds.Contains(rule.TileId) &&
                rule.IsActive &&
                rule.RuleType == RuleTypes.ExternalCompetitionMetric)
            .ToListAsync(cancellationToken);
        var changed = 0;

        foreach (var rule in rules.Where(rule => RuleTargetsCompetition(rule, competition)))
        {
            if (rule.Scope == RuleScopes.Team)
            {
                var teamScores = competition.CompetitionMode == ExternalCompetitionModes.Team
                    ? await GetTempleTeamScoresAsync(competition.Id, cancellationToken)
                    : await GetLocalTeamGroupedScoresAsync(competition, cancellationToken);

                foreach (var score in teamScores)
                {
                    changed += await ApplyExternalProgressAsync(
                        competition.EventId,
                        rule,
                        score.TeamId,
                        playerId: null,
                        score.Value,
                        syncRunId,
                        cancellationToken);
                }
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return changed;
    }

    private async Task<List<TeamScore>> GetTempleTeamScoresAsync(
        long externalCompetitionId,
        CancellationToken cancellationToken)
    {
        return await dbContext.ExternalCompetitionTeamMetrics
            .AsNoTracking()
            .Where(metric => metric.ExternalCompetitionId == externalCompetitionId &&
                metric.LocalTeamId.HasValue)
            .Select(metric => new TeamScore(metric.LocalTeamId!.Value, metric.GainedValue))
            .ToListAsync(cancellationToken);
    }

    private async Task<List<TeamScore>> GetLocalTeamGroupedScoresAsync(
        ExternalCompetition competition,
        CancellationToken cancellationToken)
    {
        return await (
                from metric in dbContext.ExternalCompetitionMetrics.AsNoTracking()
                join participant in dbContext.EventParticipants.AsNoTracking()
                    on metric.PlayerId equals participant.PlayerId
                where metric.ExternalCompetitionId == competition.Id &&
                    metric.PlayerId.HasValue &&
                    participant.EventId == competition.EventId &&
                    participant.Status == EventParticipantStatuses.Active &&
                    participant.TeamId.HasValue
                group metric by participant.TeamId!.Value
                into grouped
                select new TeamScore(grouped.Key, grouped.Sum(metric => metric.GainedValue)))
            .ToListAsync(cancellationToken);
    }

    private async Task<int> ApplyExternalProgressAsync(
        long eventId,
        TileRule rule,
        long? teamId,
        long? playerId,
        decimal currentValue,
        long syncRunId,
        CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var progress = await dbContext.EventTileProgress.SingleOrDefaultAsync(
            candidate => candidate.EventId == eventId &&
                candidate.TileId == rule.TileId &&
                candidate.TeamId == teamId &&
                candidate.PlayerId == playerId,
            cancellationToken);

        if (progress is null)
        {
            progress = new EventTileProgress
            {
                EventId = eventId,
                TileId = rule.TileId,
                TeamId = teamId,
                PlayerId = playerId,
                UpdatedAt = now
            };
            dbContext.EventTileProgress.Add(progress);
        }

        var delta = currentValue - progress.CurrentValue;
        progress.CurrentValue = currentValue;
        progress.UpdatedAt = now;
        progress.MetadataJson = JsonSerializer.Serialize(new { source = RuleTypes.ExternalCompetitionMetric, syncRunId }, JsonOptions);

        if (delta != 0)
        {
            dbContext.EventProgressContributions.Add(new EventProgressContribution
            {
                EventId = eventId,
                TileId = rule.TileId,
                TileTierId = rule.TileTierId,
                RuleId = rule.Id,
                TeamId = teamId,
                PlayerId = playerId,
                ActivityEventId = null,
                ValueAdded = delta,
                Description = "TempleOSRS sync adjustment.",
                CreatedAt = now,
                MetadataJson = JsonSerializer.Serialize(new { source = RuleTypes.ExternalCompetitionMetric, syncRunId }, JsonOptions)
            });
        }

        if (!rule.TileTierId.HasValue)
        {
            return delta == 0 ? 0 : 1;
        }

        var tileTierId = rule.TileTierId.Value;
        var tierProgress = await dbContext.EventTileTierProgress.SingleOrDefaultAsync(
            candidate => candidate.EventId == eventId &&
                candidate.TileId == rule.TileId &&
                candidate.TileTierId == tileTierId &&
                candidate.TeamId == teamId &&
                candidate.PlayerId == playerId,
            cancellationToken);

        if (tierProgress is null)
        {
            tierProgress = new EventTileTierProgress
            {
                EventId = eventId,
                TileId = rule.TileId,
                TileTierId = tileTierId,
                TeamId = teamId,
                PlayerId = playerId,
                UpdatedAt = now
            };
            dbContext.EventTileTierProgress.Add(tierProgress);
        }

        tierProgress.CurrentValue = currentValue;
        tierProgress.UpdatedAt = now;
        tierProgress.MetadataJson = JsonSerializer.Serialize(new { source = RuleTypes.ExternalCompetitionMetric, syncRunId }, JsonOptions);

        await dbContext.SaveChangesAsync(cancellationToken);

        await new TierProgressScoringService(dbContext).RecalculateTileScopeAsync(
            eventId,
            rule.TileId,
            teamId,
            playerId,
            now,
            cancellationToken);

        return delta == 0 ? 0 : 1;
    }

    private async Task<long?> FindPlayerIdAsync(string normalizedName, CancellationToken cancellationToken)
    {
        var player = await dbContext.Players.FirstOrDefaultAsync(
            candidate => candidate.RuneScapeName.ToLower() == normalizedName,
            cancellationToken);
        if (player is not null)
        {
            return player.Id;
        }

        return await dbContext.LinkedAccounts
            .Where(account => (account.Provider == IdentityProviders.TempleOsrsRuneScapeName ||
                    account.Provider == IdentityProviders.TempleOsrs ||
                    account.Provider == IdentityProviders.RuneScapeName) &&
                account.ExternalIdentifier.ToLower() == normalizedName)
            .Select(account => (long?)account.PlayerId)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private async Task<ExternalPlayerIdentity> EnsureIdentityAsync(
        string normalizedName,
        string displayName,
        long? playerId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var identity = await dbContext.ExternalPlayerIdentities.SingleOrDefaultAsync(
            candidate => candidate.Provider == IdentityProviders.TempleOsrsRuneScapeName &&
                candidate.ExternalIdentifier == normalizedName,
            cancellationToken);

        if (identity is null)
        {
            identity = new ExternalPlayerIdentity
            {
                Provider = IdentityProviders.TempleOsrsRuneScapeName,
                ExternalIdentifier = normalizedName,
                DisplayName = displayName,
                PlayerId = playerId,
                Status = playerId.HasValue ? ExternalPlayerIdentityStatuses.Matched : ExternalPlayerIdentityStatuses.Unmatched,
                FirstSeenAt = now,
                LastSeenAt = now,
                MetadataJson = JsonDefaults.Object
            };
            dbContext.ExternalPlayerIdentities.Add(identity);
            await dbContext.SaveChangesAsync(cancellationToken);
            return identity;
        }

        identity.DisplayName = displayName;
        identity.LastSeenAt = now;
        if (playerId.HasValue)
        {
            identity.PlayerId = playerId;
            identity.Status = ExternalPlayerIdentityStatuses.Matched;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return identity;
    }

    private async Task<ExternalCompetitionPlayerReview> EnsureReviewAsync(
        long externalCompetitionId,
        long identityId,
        long? playerId,
        CancellationToken cancellationToken)
    {
        var review = await dbContext.ExternalCompetitionPlayerReviews.SingleOrDefaultAsync(
            candidate => candidate.ExternalCompetitionId == externalCompetitionId &&
                candidate.ExternalPlayerIdentityId == identityId,
            cancellationToken);

        if (review is null)
        {
            review = new ExternalCompetitionPlayerReview
            {
                ExternalCompetitionId = externalCompetitionId,
                ExternalPlayerIdentityId = identityId,
                Status = playerId.HasValue
                    ? ExternalCompetitionPlayerReviewStatuses.Resolved
                    : ExternalCompetitionPlayerReviewStatuses.Unreviewed,
                MetadataJson = JsonDefaults.Object
            };
            dbContext.ExternalCompetitionPlayerReviews.Add(review);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        else if (playerId.HasValue && review.Status == ExternalCompetitionPlayerReviewStatuses.Unreviewed)
        {
            review.Status = ExternalCompetitionPlayerReviewStatuses.Resolved;
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return review;
    }

    private async Task<bool> HasActiveSyncRunAsync(
        long externalCompetitionId,
        CancellationToken cancellationToken)
    {
        return await dbContext.ExternalCompetitionSyncRuns.AnyAsync(
            candidate => candidate.ExternalCompetitionId == externalCompetitionId &&
                (candidate.Status == ExternalCompetitionSyncRunStatuses.Queued ||
                    candidate.Status == ExternalCompetitionSyncRunStatuses.Running),
            cancellationToken);
    }

    private async Task<ExternalCompetitionSyncRun> CreateSkippedRunAsync(
        long externalCompetitionId,
        string triggerType,
        CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var run = new ExternalCompetitionSyncRun
        {
            ExternalCompetitionId = externalCompetitionId,
            TriggerType = triggerType,
            RequestedAt = now,
            StartedAt = now,
            CompletedAt = now,
            Status = ExternalCompetitionSyncRunStatuses.SkippedAlreadyRunning,
            ErrorMessage = "A sync is already running for this external competition.",
            MetadataJson = JsonDefaults.Object
        };
        dbContext.ExternalCompetitionSyncRuns.Add(run);
        await dbContext.SaveChangesAsync(cancellationToken);
        return run;
    }

    private async Task<Domain.Events.EventDefinition?> FindEventAsync(
        string eventSlug,
        CancellationToken cancellationToken)
    {
        return await dbContext.Events.FirstOrDefaultAsync(
            eventDefinition => eventDefinition.Slug == eventSlug,
            cancellationToken);
    }

    private static bool RuleTargetsCompetition(TileRule rule, ExternalCompetition competition)
    {
        using var config = JsonDocument.Parse(string.IsNullOrWhiteSpace(rule.ConfigJson) ? "{}" : rule.ConfigJson);
        var root = config.RootElement;

        if (root.TryGetProperty("externalCompetitionId", out var idElement) &&
            idElement.ValueKind == JsonValueKind.Number &&
            idElement.GetInt64() != competition.Id)
        {
            return false;
        }

        return MatchesOptionalString(root, "provider", competition.Provider) &&
            MatchesOptionalString(root, "metricType", competition.MetricType) &&
            MatchesOptionalString(root, "metricKey", competition.MetricKey);
    }

    private static bool MatchesOptionalString(JsonElement element, string propertyName, string value)
    {
        return !element.TryGetProperty(propertyName, out var property) ||
            property.ValueKind != JsonValueKind.String ||
            string.Equals(property.GetString(), value, StringComparison.OrdinalIgnoreCase);
    }

    private static AdminExternalCompetitionResponse MapCompetition(ExternalCompetition competition)
    {
        return new AdminExternalCompetitionResponse(
            competition.Id,
            competition.Provider,
            competition.ExternalId,
            competition.Name,
            competition.MetricType,
            competition.MetricKey,
            competition.CompetitionMode,
            competition.Status,
            competition.LastSyncedAt,
            competition.LastSuccessfulSyncAt,
            competition.LastSyncStatus,
            competition.LastSyncError);
    }

    private static EventTempleRefreshCompetitionResponse MapRefreshCompetition(
        ExternalCompetition competition,
        bool refreshRequested,
        string status,
        string message,
        DateTimeOffset? nextRefreshAvailableAt)
    {
        return new EventTempleRefreshCompetitionResponse(
            competition.Id,
            competition.Name,
            competition.ExternalId,
            status,
            refreshRequested,
            competition.LastSuccessfulSyncAt,
            nextRefreshAvailableAt,
            message);
    }

    private static AdminExternalCompetitionSyncRunResponse MapRun(ExternalCompetitionSyncRun run)
    {
        return new AdminExternalCompetitionSyncRunResponse(
            run.Id,
            run.ExternalCompetitionId,
            run.Status,
            run.TriggerType,
            run.RequestedAt,
            run.StartedAt,
            run.CompletedAt,
            run.RowsRead,
            run.RowsChanged,
            run.ErrorMessage);
    }

    private static AdminEventSetupSummaryResponse MapEvent(Domain.Events.EventDefinition eventDefinition)
    {
        return new AdminEventSetupSummaryResponse(
            eventDefinition.Id,
            eventDefinition.Slug,
            eventDefinition.Name,
            eventDefinition.Status,
            eventDefinition.EventType,
            eventDefinition.StartsAt,
            eventDefinition.EndsAt,
            eventDefinition.TimeZone);
    }

    private static EventSummaryResponse MapEventSummary(Domain.Events.EventDefinition eventDefinition)
    {
        return new EventSummaryResponse(
            eventDefinition.Id,
            eventDefinition.Slug,
            eventDefinition.Name,
            eventDefinition.EventType,
            eventDefinition.Status,
            eventDefinition.StartsAt,
            eventDefinition.EndsAt,
            eventDefinition.TimeZone);
    }

    private static string GetPublicRefreshMessage(AdminExternalCompetitionSyncRunResponse? run)
    {
        return run?.Status switch
        {
            ExternalCompetitionSyncRunStatuses.Succeeded => "TempleOSRS refresh completed.",
            ExternalCompetitionSyncRunStatuses.Failed => "TempleOSRS refresh failed. Showing the last cached progress.",
            ExternalCompetitionSyncRunStatuses.SkippedAlreadyRunning => "A TempleOSRS refresh is already running.",
            null => "TempleOSRS refresh was not started.",
            _ => "TempleOSRS refresh request was accepted."
        };
    }

    private static string FormatCooldown(TimeSpan remaining)
    {
        var seconds = Math.Max(1, (int)Math.Ceiling(remaining.TotalSeconds));
        return seconds >= 60
            ? $"in {Math.Ceiling(seconds / 60m):0} minute(s)"
            : $"in {seconds} second(s)";
    }

    private static string NormalizeMetric(string? value, string fallback)
    {
        return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim().ToLowerInvariant();
    }

    private static string NormalizeIdentity(string value)
    {
        return value.Trim().ToLowerInvariant();
    }

    private static string SafeError(Exception exception)
    {
        return exception.GetType().Name == nameof(HttpRequestException)
            ? "TempleOSRS request failed."
            : exception.Message;
    }

    private static IReadOnlyList<string> DeserializeMembers(string membersJson)
    {
        try
        {
            return JsonSerializer.Deserialize<IReadOnlyList<string>>(membersJson, JsonOptions) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private sealed record TeamScore(long TeamId, long Value);
}

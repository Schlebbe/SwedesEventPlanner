using SwedesEventPlanner.Contracts.Admin;
using SwedesEventPlanner.Contracts.Events;

namespace SwedesEventPlanner.Application.ExternalCompetitions;

public interface IExternalCompetitionSyncService
{
    Task<AdminExternalCompetitionResponse?> LinkTempleCompetitionAsync(
        string eventSlug,
        LinkExternalCompetitionRequest request,
        CancellationToken cancellationToken);

    Task<AdminExternalCompetitionListResponse?> ListCompetitionsAsync(
        string eventSlug,
        CancellationToken cancellationToken);

    Task<AdminExternalCompetitionSyncRunResponse?> SyncCompetitionAsync(
        string eventSlug,
        long externalCompetitionId,
        CancellationToken cancellationToken);

    Task<EventTempleRefreshResponse?> RequestPublicRefreshAsync(
        string eventSlug,
        CancellationToken cancellationToken);

    Task<int> SyncDueActiveCompetitionsAsync(CancellationToken cancellationToken);

    Task<AdminExternalCompetitionSyncRunListResponse> ListSyncRunsAsync(
        long externalCompetitionId,
        CancellationToken cancellationToken);

    Task<AdminExternalCompetitionPlayerMetricListResponse> ListPlayerMetricsAsync(
        long externalCompetitionId,
        CancellationToken cancellationToken);

    Task<AdminExternalCompetitionTeamMetricListResponse> ListTeamMetricsAsync(
        long externalCompetitionId,
        CancellationToken cancellationToken);

    Task<AdminExternalCompetitionUnmatchedIdentityListResponse> ListUnmatchedIdentitiesAsync(
        long externalCompetitionId,
        CancellationToken cancellationToken);
}

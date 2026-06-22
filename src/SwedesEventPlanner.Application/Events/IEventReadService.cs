using SwedesEventPlanner.Contracts.Events;

namespace SwedesEventPlanner.Application.Events;

public interface IEventReadService
{
    Task<EventListResponse> ListEventsAsync(CancellationToken cancellationToken);

    Task<EventSummaryResponse?> GetEventAsync(
        string slug,
        CancellationToken cancellationToken);

    Task<EventBoardResponse?> GetBoardAsync(
        string slug,
        CancellationToken cancellationToken);

    Task<EventTeamBoardResponse?> GetTeamBoardAsync(
        string slug,
        long teamId,
        CancellationToken cancellationToken);

    Task<EventTeamListResponse?> GetTeamsAsync(
        string slug,
        CancellationToken cancellationToken);

    Task<EventContributionListResponse?> GetContributionsAsync(
        string slug,
        int limit,
        CancellationToken cancellationToken);
}

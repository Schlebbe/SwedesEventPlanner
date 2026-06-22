namespace SwedesEventPlanner.Contracts.Events;

/// <summary>Public read-only TempleOSRS refresh status for an event.</summary>
public sealed record EventTempleRefreshResponse(
    EventSummaryResponse Event,
    IReadOnlyList<EventTempleRefreshCompetitionResponse> Competitions);

/// <summary>Public read-only TempleOSRS refresh status for one linked competition.</summary>
public sealed record EventTempleRefreshCompetitionResponse(
    long Id,
    string Name,
    string ExternalId,
    string Status,
    bool RefreshRequested,
    DateTimeOffset? LastSuccessfulSyncAt,
    DateTimeOffset? NextRefreshAvailableAt,
    string Message);

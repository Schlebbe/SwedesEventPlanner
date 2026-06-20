namespace SwedesEventPlanner.Contracts.Events;

/// <summary>Contains public team summaries for an event.</summary>
public sealed record EventTeamListResponse(
    EventSummaryResponse Event,
    IReadOnlyList<EventTeamSummaryResponse> Teams);

/// <summary>Represents a public team score and progress summary.</summary>
public sealed record EventTeamSummaryResponse(
    long Id,
    string Name,
    int Score,
    int ScoredTiers,
    int CompletedTiles,
    decimal CurrentValue,
    int ContributionCount);

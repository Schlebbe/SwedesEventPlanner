namespace SwedesEventPlanner.Contracts.Events;

/// <summary>Contains recent public progress contributions for an event.</summary>
public sealed record EventContributionListResponse(
    EventSummaryResponse Event,
    IReadOnlyList<EventContributionResponse> Contributions);

/// <summary>Represents a public progress contribution.</summary>
public sealed record EventContributionResponse(
    long Id,
    string PlayerName,
    long? TeamId,
    string? TeamName,
    string TileTitle,
    string? TierTitle,
    decimal ValueAdded,
    string? Description,
    DateTimeOffset CreatedAt);

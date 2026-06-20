namespace SwedesEventPlanner.Contracts.Events;

/// <summary>Contains public event summaries.</summary>
public sealed record EventListResponse(IReadOnlyList<EventSummaryResponse> Events);

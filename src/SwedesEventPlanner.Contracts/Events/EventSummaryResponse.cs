namespace SwedesEventPlanner.Contracts.Events;

/// <summary>Represents a public event summary.</summary>
public sealed record EventSummaryResponse(
    long Id,
    string Slug,
    string Name,
    string EventType,
    string Status,
    DateTimeOffset StartsAt,
    DateTimeOffset? EndsAt,
    string TimeZone);

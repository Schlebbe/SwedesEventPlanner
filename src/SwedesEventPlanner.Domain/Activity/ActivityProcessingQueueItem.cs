namespace SwedesEventPlanner.Domain.Activity;

public sealed class ActivityProcessingQueueItem
{
    public long Id { get; set; }

    public long ActivityEventId { get; set; }

    public string Status { get; set; } = ActivityProcessingStatuses.Pending;

    public int Attempts { get; set; }

    public DateTimeOffset AvailableAt { get; set; }

    public DateTimeOffset? LockedAt { get; set; }

    public DateTimeOffset? ProcessedAt { get; set; }

    public string? ErrorMessage { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}

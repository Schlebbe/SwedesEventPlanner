using SwedesEventPlanner.Domain.Common;

namespace SwedesEventPlanner.Domain.Activity;

public sealed class ActivityEvent
{
    public long Id { get; set; }

    public long PlayerId { get; set; }

    public required string ActivityType { get; set; }

    public string? SourceSystem { get; set; }

    public string? SourceEndpoint { get; set; }

    public string? SourcePayloadVersion { get; set; }

    public string? AccountProfileType { get; set; }

    public string? Source { get; set; }

    public int? ItemId { get; set; }

    public string? ItemName { get; set; }

    public int? Quantity { get; set; }

    public string? Skill { get; set; }

    public long? Xp { get; set; }

    public string? BossName { get; set; }

    public int? Kc { get; set; }

    public DateTimeOffset OccurredAt { get; set; }

    public DateTimeOffset ReceivedAt { get; set; }

    public string RawPayloadJson { get; set; } = JsonDefaults.Object;

    public string? DedupeKey { get; set; }
}

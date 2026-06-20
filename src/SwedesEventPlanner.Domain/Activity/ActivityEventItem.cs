using SwedesEventPlanner.Domain.Common;

namespace SwedesEventPlanner.Domain.Activity;

public sealed class ActivityEventItem
{
    public long Id { get; set; }

    public long ActivityEventId { get; set; }

    public int ItemId { get; set; }

    public string? ItemName { get; set; }

    public int Quantity { get; set; } = 1;

    public string? Source { get; set; }

    public string MetadataJson { get; set; } = JsonDefaults.Object;
}

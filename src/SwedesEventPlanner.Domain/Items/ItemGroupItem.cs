using SwedesEventPlanner.Domain.Common;

namespace SwedesEventPlanner.Domain.Items;

public sealed class ItemGroupItem
{
    public long Id { get; set; }

    public long ItemGroupId { get; set; }

    public int ItemId { get; set; }

    public required string ItemName { get; set; }

    public string MetadataJson { get; set; } = JsonDefaults.Object;
}

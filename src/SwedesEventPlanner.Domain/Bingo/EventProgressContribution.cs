using SwedesEventPlanner.Domain.Common;

namespace SwedesEventPlanner.Domain.Bingo;

public sealed class EventProgressContribution
{
    public long Id { get; set; }

    public long EventId { get; set; }

    public long TileId { get; set; }

    public long? TileTierId { get; set; }

    public long RuleId { get; set; }

    public long? TeamId { get; set; }

    public long PlayerId { get; set; }

    public long ActivityEventId { get; set; }

    public decimal ValueAdded { get; set; }

    public string? Description { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public string MetadataJson { get; set; } = JsonDefaults.Object;
}

using SwedesEventPlanner.Domain.Common;

namespace SwedesEventPlanner.Domain.Bingo;

public sealed class EventTileProgress
{
    public long Id { get; set; }

    public long EventId { get; set; }

    public long TileId { get; set; }

    public long? TeamId { get; set; }

    public long? PlayerId { get; set; }

    public decimal CurrentValue { get; set; }

    public int CurrentTier { get; set; }

    public bool IsCompleted { get; set; }

    public DateTimeOffset? CompletedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public string MetadataJson { get; set; } = JsonDefaults.Object;
}

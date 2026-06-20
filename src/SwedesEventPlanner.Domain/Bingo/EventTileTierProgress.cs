using SwedesEventPlanner.Domain.Common;

namespace SwedesEventPlanner.Domain.Bingo;

public sealed class EventTileTierProgress
{
    public long Id { get; set; }

    public long EventId { get; set; }

    public long TileId { get; set; }

    public long TileTierId { get; set; }

    public long? TeamId { get; set; }

    public long? PlayerId { get; set; }

    public decimal CurrentValue { get; set; }

    public bool IsAchieved { get; set; }

    public DateTimeOffset? AchievedAt { get; set; }

    public bool IsScored { get; set; }

    public DateTimeOffset? ScoredAt { get; set; }

    public int ScoreAwarded { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public string MetadataJson { get; set; } = JsonDefaults.Object;
}

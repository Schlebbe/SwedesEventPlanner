using SwedesEventPlanner.Domain.Common;

namespace SwedesEventPlanner.Domain.Bingo;

public sealed class TileUnlockCondition
{
    public long Id { get; set; }

    public long TileId { get; set; }

    public required string ConditionType { get; set; }

    public string ConfigJson { get; set; } = JsonDefaults.Object;

    public DateTimeOffset CreatedAt { get; set; }
}

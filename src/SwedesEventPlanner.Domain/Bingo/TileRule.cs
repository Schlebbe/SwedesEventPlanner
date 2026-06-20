using SwedesEventPlanner.Domain.Common;

namespace SwedesEventPlanner.Domain.Bingo;

public sealed class TileRule
{
    public long Id { get; set; }

    public long TileId { get; set; }

    public long? TileTierId { get; set; }

    public required string RuleType { get; set; }

    public required string Scope { get; set; }

    public bool IsActive { get; set; } = true;

    public string ConfigJson { get; set; } = JsonDefaults.Object;

    public DateTimeOffset CreatedAt { get; set; }
}

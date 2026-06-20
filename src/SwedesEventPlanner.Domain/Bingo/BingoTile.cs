using SwedesEventPlanner.Domain.Common;

namespace SwedesEventPlanner.Domain.Bingo;

public sealed class BingoTile
{
    public long Id { get; set; }

    public long BoardId { get; set; }

    public required string Title { get; set; }

    public string? Description { get; set; }

    public int? PositionX { get; set; }

    public int? PositionY { get; set; }

    public int SortOrder { get; set; }

    public string ConfigJson { get; set; } = JsonDefaults.Object;
}

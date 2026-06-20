using SwedesEventPlanner.Domain.Common;

namespace SwedesEventPlanner.Domain.Bingo;

public sealed class BingoTileTier
{
    public long Id { get; set; }

    public long TileId { get; set; }

    public int TierNumber { get; set; }

    public string? Title { get; set; }

    public string? Description { get; set; }

    public int ScoreValue { get; set; } = 1;

    public bool IsRequiredForBoardCompletion { get; set; } = true;

    public int SortOrder { get; set; }

    public string ConfigJson { get; set; } = JsonDefaults.Object;
}

using SwedesEventPlanner.Domain.Common;

namespace SwedesEventPlanner.Domain.Bingo;

public sealed class BingoBoard
{
    public long Id { get; set; }

    public long EventId { get; set; }

    public required string Name { get; set; }

    public int? Rows { get; set; }

    public int? Columns { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public string ConfigJson { get; set; } = JsonDefaults.Object;
}

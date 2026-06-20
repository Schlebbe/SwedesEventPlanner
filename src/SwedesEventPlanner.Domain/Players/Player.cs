namespace SwedesEventPlanner.Domain.Players;

public sealed class Player
{
    public long Id { get; set; }

    public required string DisplayName { get; set; }

    public required string RuneScapeName { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}

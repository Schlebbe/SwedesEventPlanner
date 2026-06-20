namespace SwedesEventPlanner.Domain.Items;

public sealed class ItemGroup
{
    public long Id { get; set; }

    public required string Key { get; set; }

    public required string Name { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}

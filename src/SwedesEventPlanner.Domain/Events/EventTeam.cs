using SwedesEventPlanner.Domain.Common;

namespace SwedesEventPlanner.Domain.Events;

public sealed class EventTeam
{
    public long Id { get; set; }

    public long EventId { get; set; }

    public required string Name { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public string ConfigJson { get; set; } = JsonDefaults.Object;
}

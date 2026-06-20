namespace SwedesEventPlanner.Domain.Events;

public sealed class EventDefinition
{
    public long Id { get; set; }

    public required string Slug { get; set; }

    public required string Name { get; set; }

    public required string EventType { get; set; }

    public required string Status { get; set; }

    public DateTimeOffset StartsAt { get; set; }

    public DateTimeOffset? EndsAt { get; set; }

    public string TimeZone { get; set; } = EventDefaults.TimeZone;

    public DateTimeOffset CreatedAt { get; set; }

    public string ConfigJson { get; set; } = "{}";
}

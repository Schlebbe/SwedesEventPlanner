using SwedesEventPlanner.Domain.Common;

namespace SwedesEventPlanner.Domain.Events;

public sealed class EventParticipant
{
    public long Id { get; set; }

    public long EventId { get; set; }

    public long PlayerId { get; set; }

    public long? TeamId { get; set; }

    public DateTimeOffset JoinedAt { get; set; }

    public string Status { get; set; } = EventParticipantStatuses.Active;

    public string ConfigJson { get; set; } = JsonDefaults.Object;
}

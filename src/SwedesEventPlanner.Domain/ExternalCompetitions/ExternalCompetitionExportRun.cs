using SwedesEventPlanner.Domain.Common;

namespace SwedesEventPlanner.Domain.ExternalCompetitions;

public sealed class ExternalCompetitionExportRun
{
    public long Id { get; set; }

    public long ExternalCompetitionId { get; set; }

    public long EventId { get; set; }

    public required string TriggerType { get; set; }

    public string? TriggeredBy { get; set; }

    public DateTimeOffset RequestedAt { get; set; }

    public DateTimeOffset? StartedAt { get; set; }

    public DateTimeOffset? CompletedAt { get; set; }

    public required string Status { get; set; }

    public int? ParticipantsIntended { get; set; }

    public int? ParticipantsAdded { get; set; }

    public int? ParticipantsRemoved { get; set; }

    public int? TeamMappingsIntended { get; set; }

    public string? ErrorMessage { get; set; }

    public string RequestSummaryJson { get; set; } = JsonDefaults.Object;

    public string? ResponseSummaryJson { get; set; }

    public string? ValidationSummaryJson { get; set; }

    public string MetadataJson { get; set; } = JsonDefaults.Object;
}

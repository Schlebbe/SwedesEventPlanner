using SwedesEventPlanner.Domain.Common;

namespace SwedesEventPlanner.Domain.ExternalCompetitions;

public sealed class ExternalCompetitionSyncRun
{
    public long Id { get; set; }

    public long ExternalCompetitionId { get; set; }

    public required string TriggerType { get; set; }

    public string? TriggeredBy { get; set; }

    public DateTimeOffset? RequestedAt { get; set; }

    public DateTimeOffset StartedAt { get; set; }

    public DateTimeOffset? CompletedAt { get; set; }

    public required string Status { get; set; }

    public int? RowsRead { get; set; }

    public int? RowsChanged { get; set; }

    public string? ErrorMessage { get; set; }

    public string? RawResponseJson { get; set; }

    public string MetadataJson { get; set; } = JsonDefaults.Object;
}

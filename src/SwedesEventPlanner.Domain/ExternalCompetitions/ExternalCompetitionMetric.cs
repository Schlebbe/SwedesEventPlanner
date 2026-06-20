using SwedesEventPlanner.Domain.Common;

namespace SwedesEventPlanner.Domain.ExternalCompetitions;

public sealed class ExternalCompetitionMetric
{
    public long Id { get; set; }

    public long ExternalCompetitionId { get; set; }

    public long? ExternalPlayerIdentityId { get; set; }

    public long? ExternalCompetitionPlayerReviewId { get; set; }

    public long? PlayerId { get; set; }

    public required string RuneScapeName { get; set; }

    public required string MetricType { get; set; }

    public required string MetricKey { get; set; }

    public long? StartValue { get; set; }

    public long? CurrentValue { get; set; }

    public long GainedValue { get; set; }

    public int? Rank { get; set; }

    public DateTimeOffset LastSyncedAt { get; set; }

    public string MetadataJson { get; set; } = JsonDefaults.Object;
}

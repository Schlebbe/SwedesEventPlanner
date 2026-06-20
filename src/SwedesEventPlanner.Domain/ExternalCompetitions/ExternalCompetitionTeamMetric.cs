using SwedesEventPlanner.Domain.Common;

namespace SwedesEventPlanner.Domain.ExternalCompetitions;

public sealed class ExternalCompetitionTeamMetric
{
    public long Id { get; set; }

    public long ExternalCompetitionId { get; set; }

    public long? LocalTeamId { get; set; }

    public required string TempleTeamKey { get; set; }

    public required string TeamName { get; set; }

    public required string MetricType { get; set; }

    public required string MetricKey { get; set; }

    public long? StartValue { get; set; }

    public long? CurrentValue { get; set; }

    public long GainedValue { get; set; }

    public int? Rank { get; set; }

    public string? MvpRuneScapeName { get; set; }

    public string MembersJson { get; set; } = JsonDefaults.Array;

    public DateTimeOffset LastSyncedAt { get; set; }

    public string MetadataJson { get; set; } = JsonDefaults.Object;
}

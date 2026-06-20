using SwedesEventPlanner.Domain.Common;

namespace SwedesEventPlanner.Domain.ExternalCompetitions;

public sealed class ExternalCompetition
{
    public long Id { get; set; }

    public long EventId { get; set; }

    public required string Provider { get; set; }

    public required string ExternalId { get; set; }

    public required string Name { get; set; }

    public required string MetricType { get; set; }

    public required string MetricKey { get; set; }

    public string CompetitionMode { get; set; } = ExternalCompetitionModes.Unknown;

    public string? SecretReference { get; set; }

    public string Status { get; set; } = ExternalCompetitionStatuses.Active;

    public DateTimeOffset? LastSyncedAt { get; set; }

    public DateTimeOffset? LastSuccessfulSyncAt { get; set; }

    public DateTimeOffset? LastPublicSyncRequestAcceptedAt { get; set; }

    public string? LastSyncStatus { get; set; }

    public string? LastSyncError { get; set; }

    public DateTimeOffset? NextPublicSyncAvailableAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public string ConfigJson { get; set; } = JsonDefaults.Object;
}

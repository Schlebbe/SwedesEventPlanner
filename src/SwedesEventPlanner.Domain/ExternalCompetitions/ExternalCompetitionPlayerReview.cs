using SwedesEventPlanner.Domain.Common;

namespace SwedesEventPlanner.Domain.ExternalCompetitions;

public sealed class ExternalCompetitionPlayerReview
{
    public long Id { get; set; }

    public long ExternalCompetitionId { get; set; }

    public long ExternalPlayerIdentityId { get; set; }

    public string Status { get; set; } = ExternalCompetitionPlayerReviewStatuses.Unreviewed;

    public DateTimeOffset? IgnoredAt { get; set; }

    public DateTimeOffset? ReviewedAt { get; set; }

    public string? ReviewedBy { get; set; }

    public string MetadataJson { get; set; } = JsonDefaults.Object;
}

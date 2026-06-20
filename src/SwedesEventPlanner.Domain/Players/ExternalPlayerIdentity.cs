using SwedesEventPlanner.Domain.Common;

namespace SwedesEventPlanner.Domain.Players;

public sealed class ExternalPlayerIdentity
{
    public long Id { get; set; }

    public required string Provider { get; set; }

    public required string ExternalIdentifier { get; set; }

    public required string DisplayName { get; set; }

    public long? PlayerId { get; set; }

    public string Status { get; set; } = ExternalPlayerIdentityStatuses.Unmatched;

    public DateTimeOffset FirstSeenAt { get; set; }

    public DateTimeOffset LastSeenAt { get; set; }

    public DateTimeOffset? ReviewedAt { get; set; }

    public string? ReviewedBy { get; set; }

    public string MetadataJson { get; set; } = JsonDefaults.Object;
}

using SwedesEventPlanner.Domain.Common;

namespace SwedesEventPlanner.Domain.Players;

public sealed class LinkedAccount
{
    public long Id { get; set; }

    public long PlayerId { get; set; }

    public required string Provider { get; set; }

    public required string ExternalIdentifier { get; set; }

    public string? DisplayName { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public string MetadataJson { get; set; } = JsonDefaults.Object;
}

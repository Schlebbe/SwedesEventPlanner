namespace SwedesEventPlanner.Contracts.Activity;

/// <summary>Payload for the mock/dev activity ingestion endpoint.</summary>
public sealed record CreateActivityRequest
{
    /// <summary>RuneScape name for the known local player.</summary>
    public required string PlayerName { get; init; }

    /// <summary>Activity type, such as item_drop.</summary>
    public required string ActivityType { get; init; }

    /// <summary>Optional source, such as Theatre of Blood.</summary>
    public string? Source { get; init; }

    /// <summary>Optional item identifier for item activity.</summary>
    public int? ItemId { get; init; }

    /// <summary>Optional item display name.</summary>
    public string? ItemName { get; init; }

    /// <summary>Optional item quantity. Defaults to one for item drops.</summary>
    public int? Quantity { get; init; }

    /// <summary>Optional skill name for future XP snapshot activity.</summary>
    public string? Skill { get; init; }

    /// <summary>Optional XP value for future XP snapshot activity.</summary>
    public long? Xp { get; init; }

    /// <summary>Optional boss name for future KC snapshot activity.</summary>
    public string? BossName { get; init; }

    /// <summary>Optional kill count for future KC snapshot activity.</summary>
    public int? Kc { get; init; }

    /// <summary>When the game activity occurred.</summary>
    public DateTimeOffset OccurredAt { get; init; }

    /// <summary>Optional caller-provided idempotency key.</summary>
    public string? DedupeKey { get; init; }
}

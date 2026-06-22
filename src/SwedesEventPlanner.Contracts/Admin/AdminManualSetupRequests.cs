using System.ComponentModel.DataAnnotations;

namespace SwedesEventPlanner.Contracts.Admin;

/// <summary>Payload for creating an event from the admin/testing setup workflow.</summary>
public sealed record CreateAdminEventRequest
{
    /// <summary>Public URL slug for the event.</summary>
    [Required]
    public required string Slug { get; init; }

    /// <summary>Display name for the event.</summary>
    [Required]
    public required string Name { get; init; }

    /// <summary>Event type. Defaults to bingo.</summary>
    public string EventType { get; init; } = "bingo";

    /// <summary>Event status. Defaults to draft.</summary>
    public string Status { get; init; } = "draft";

    /// <summary>Event start time.</summary>
    public DateTimeOffset? StartsAt { get; init; }

    /// <summary>Optional event end time.</summary>
    public DateTimeOffset? EndsAt { get; init; }

    /// <summary>Event time zone. Defaults to Europe/Stockholm.</summary>
    public string? TimeZone { get; init; }
}

/// <summary>Payload for updating basic event fields.</summary>
public sealed record UpdateAdminEventRequest
{
    /// <summary>Display name for the event.</summary>
    [Required]
    public required string Name { get; init; }

    /// <summary>Event type.</summary>
    public string EventType { get; init; } = "bingo";

    /// <summary>Event start time.</summary>
    public DateTimeOffset StartsAt { get; init; }

    /// <summary>Optional event end time.</summary>
    public DateTimeOffset? EndsAt { get; init; }

    /// <summary>Event time zone.</summary>
    public string? TimeZone { get; init; }
}

/// <summary>Payload for changing an event status.</summary>
public sealed record UpdateAdminEventStatusRequest
{
    /// <summary>New event status.</summary>
    [Required]
    public required string Status { get; init; }
}

/// <summary>Payload for creating a bingo board.</summary>
public sealed record CreateBingoBoardRequest
{
    /// <summary>Board name.</summary>
    [Required]
    public required string Name { get; init; }

    /// <summary>Optional row count.</summary>
    public int? Rows { get; init; }

    /// <summary>Optional column count.</summary>
    public int? Columns { get; init; }
}

/// <summary>Payload for creating a bingo tile.</summary>
public sealed record CreateBingoTileRequest
{
    /// <summary>Tile title.</summary>
    [Required]
    public required string Title { get; init; }

    /// <summary>Optional tile description.</summary>
    public string? Description { get; init; }

    /// <summary>Optional board X position.</summary>
    public int? PositionX { get; init; }

    /// <summary>Optional board Y position.</summary>
    public int? PositionY { get; init; }

    /// <summary>Tile sort order.</summary>
    public int SortOrder { get; init; }
}

/// <summary>Payload for creating a bingo tile tier.</summary>
public sealed record CreateBingoTileTierRequest
{
    /// <summary>Tier number.</summary>
    public int TierNumber { get; init; }

    /// <summary>Optional tier title.</summary>
    public string? Title { get; init; }

    /// <summary>Optional tier description.</summary>
    public string? Description { get; init; }

    /// <summary>Score awarded when the tier is scored.</summary>
    public int ScoreValue { get; init; } = 1;

    /// <summary>Whether this tier is required for board completion.</summary>
    public bool IsRequiredForBoardCompletion { get; init; } = true;

    /// <summary>Tier sort order.</summary>
    public int SortOrder { get; init; }
}

/// <summary>Payload for creating or updating a tile rule.</summary>
public sealed record UpsertTileRuleRequest
{
    /// <summary>Optional tier ID this rule contributes to.</summary>
    public long? TileTierId { get; init; }

    /// <summary>Rule type such as item_count, point_threshold, external_competition_metric, or manual.</summary>
    [Required]
    public required string RuleType { get; init; }

    /// <summary>Rule scope: team, player, or event.</summary>
    public string Scope { get; init; } = "team";

    /// <summary>Whether the rule should be evaluated.</summary>
    public bool IsActive { get; init; } = true;

    /// <summary>Rule configuration JSON object.</summary>
    [Required]
    public required string ConfigJson { get; init; }
}

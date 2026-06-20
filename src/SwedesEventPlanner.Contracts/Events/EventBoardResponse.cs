namespace SwedesEventPlanner.Contracts.Events;

/// <summary>Represents a public event board with tile, tier, and team progress.</summary>
public sealed record EventBoardResponse(
    EventSummaryResponse Event,
    BoardResponse Board,
    IReadOnlyList<EventBoardTeamResponse> Teams);

/// <summary>Represents a bingo board.</summary>
public sealed record BoardResponse(
    long Id,
    string Name,
    int? Rows,
    int? Columns,
    IReadOnlyList<BoardTileResponse> Tiles);

/// <summary>Represents a board tile and its visible progress.</summary>
public sealed record BoardTileResponse(
    long Id,
    string Title,
    string? Description,
    int? PositionX,
    int? PositionY,
    int SortOrder,
    IReadOnlyList<BoardTileTeamProgressResponse> TeamProgress,
    IReadOnlyList<BoardTileTierResponse> Tiers);

/// <summary>Represents current team progress for a tile.</summary>
public sealed record BoardTileTeamProgressResponse(
    long TeamId,
    string TeamName,
    decimal CurrentValue,
    int CurrentTier,
    bool IsCompleted,
    DateTimeOffset? CompletedAt);

/// <summary>Represents a board tile tier and its visible team progress.</summary>
public sealed record BoardTileTierResponse(
    long Id,
    int TierNumber,
    string? Title,
    string? Description,
    int ScoreValue,
    bool IsRequiredForBoardCompletion,
    decimal? RequiredValue,
    IReadOnlyList<BoardTileTierTeamProgressResponse> TeamProgress);

/// <summary>Represents current team progress for a tile tier.</summary>
public sealed record BoardTileTierTeamProgressResponse(
    long TeamId,
    string TeamName,
    decimal CurrentValue,
    bool IsAchieved,
    DateTimeOffset? AchievedAt,
    bool IsScored,
    DateTimeOffset? ScoredAt,
    int ScoreAwarded);

/// <summary>Represents a team shown on an event board.</summary>
public sealed record EventBoardTeamResponse(
    long Id,
    string Name,
    int Score,
    int ScoredTiers,
    int CompletedTiles,
    decimal CurrentValue);

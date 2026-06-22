namespace SwedesEventPlanner.Contracts.Admin;

/// <summary>Admin-facing event summary for setup workflows.</summary>
public sealed record AdminEventSetupSummaryResponse(
    long Id,
    string Slug,
    string Name,
    string Status,
    string EventType,
    DateTimeOffset StartsAt,
    DateTimeOffset? EndsAt,
    string TimeZone);

/// <summary>Admin-facing imported signup row.</summary>
public sealed record AdminEventSignupResponse(
    long Id,
    long? PlayerId,
    string RuneScapeName,
    string? DisplayName,
    string? AvailabilityText,
    decimal? DailyHours,
    string? PreferredContent,
    string? TeamPreference,
    string? Notes,
    string Status,
    string SourceSystem,
    DateTimeOffset? SubmittedAt,
    DateTimeOffset ImportedAt);

/// <summary>Admin-facing signup list for an event.</summary>
public sealed record AdminEventSignupListResponse(
    AdminEventSetupSummaryResponse Event,
    IReadOnlyList<AdminEventSignupResponse> Signups);

/// <summary>Admin-facing team row for roster setup.</summary>
public sealed record AdminEventTeamResponse(
    long Id,
    string Name,
    int ParticipantCount);

/// <summary>Admin-facing event participant row.</summary>
public sealed record AdminEventParticipantResponse(
    long Id,
    long PlayerId,
    string DisplayName,
    string RuneScapeName,
    long? TeamId,
    string? TeamName,
    string Status,
    DateTimeOffset JoinedAt,
    bool IsUnassigned);

/// <summary>Admin-facing participant and team roster for event setup.</summary>
public sealed record AdminEventParticipantListResponse(
    AdminEventSetupSummaryResponse Event,
    IReadOnlyList<AdminEventTeamResponse> Teams,
    IReadOnlyList<AdminEventParticipantResponse> Participants,
    int UnassignedCount);

/// <summary>Admin request for linking a TempleOSRS competition to an event.</summary>
public sealed record LinkExternalCompetitionRequest
{
    public required string ExternalId { get; init; }

    public string? Name { get; init; }

    public string MetricType { get; init; } = "xp";

    public string MetricKey { get; init; } = "overall";
}

/// <summary>Admin-facing linked external competition row.</summary>
public sealed record AdminExternalCompetitionResponse(
    long Id,
    string Provider,
    string ExternalId,
    string Name,
    string MetricType,
    string MetricKey,
    string CompetitionMode,
    string Status,
    DateTimeOffset? LastSyncedAt,
    DateTimeOffset? LastSuccessfulSyncAt,
    string? LastSyncStatus,
    string? LastSyncError);

/// <summary>Admin-facing list of linked external competitions.</summary>
public sealed record AdminExternalCompetitionListResponse(
    AdminEventSetupSummaryResponse Event,
    IReadOnlyList<AdminExternalCompetitionResponse> Competitions);

/// <summary>Admin-facing sync run row for an external competition.</summary>
public sealed record AdminExternalCompetitionSyncRunResponse(
    long Id,
    long ExternalCompetitionId,
    string Status,
    string TriggerType,
    DateTimeOffset? RequestedAt,
    DateTimeOffset StartedAt,
    DateTimeOffset? CompletedAt,
    int? RowsRead,
    int? RowsChanged,
    string? ErrorMessage);

/// <summary>Admin-facing sync run list.</summary>
public sealed record AdminExternalCompetitionSyncRunListResponse(
    IReadOnlyList<AdminExternalCompetitionSyncRunResponse> Runs);

/// <summary>Admin-facing cached player metric row.</summary>
public sealed record AdminExternalCompetitionPlayerMetricResponse(
    long Id,
    string RuneScapeName,
    long? PlayerId,
    string? LocalPlayerName,
    string MetricType,
    string MetricKey,
    long? StartValue,
    long? CurrentValue,
    long GainedValue,
    int? Rank,
    DateTimeOffset LastSyncedAt);

/// <summary>Admin-facing cached player metric list.</summary>
public sealed record AdminExternalCompetitionPlayerMetricListResponse(
    IReadOnlyList<AdminExternalCompetitionPlayerMetricResponse> Metrics);

/// <summary>Admin-facing cached team metric row.</summary>
public sealed record AdminExternalCompetitionTeamMetricResponse(
    long Id,
    string TempleTeamKey,
    string TeamName,
    long? LocalTeamId,
    string? LocalTeamName,
    string MetricType,
    string MetricKey,
    long? StartValue,
    long? CurrentValue,
    long GainedValue,
    int? Rank,
    string? MvpRuneScapeName,
    IReadOnlyList<string> Members,
    DateTimeOffset LastSyncedAt,
    bool HasLocalTeamMismatch);

/// <summary>Admin-facing cached team metric list.</summary>
public sealed record AdminExternalCompetitionTeamMetricListResponse(
    IReadOnlyList<AdminExternalCompetitionTeamMetricResponse> Metrics);

/// <summary>Admin-facing unmatched Temple identity row.</summary>
public sealed record AdminExternalCompetitionUnmatchedIdentityResponse(
    long Id,
    string RuneScapeName,
    string DisplayName,
    DateTimeOffset FirstSeenAt,
    DateTimeOffset LastSeenAt);

/// <summary>Admin-facing unmatched Temple identity list.</summary>
public sealed record AdminExternalCompetitionUnmatchedIdentityListResponse(
    IReadOnlyList<AdminExternalCompetitionUnmatchedIdentityResponse> Identities);

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

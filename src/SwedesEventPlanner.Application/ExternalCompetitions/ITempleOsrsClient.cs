namespace SwedesEventPlanner.Application.ExternalCompetitions;

public interface ITempleOsrsClient
{
    Task<TempleOsrsCompetitionInfo> GetCompetitionInfoAsync(
        string competitionId,
        CancellationToken cancellationToken);
}

public sealed record TempleOsrsCompetitionInfo(
    string Id,
    string Name,
    bool IsTeamCompetition,
    string MetricKey,
    string? Status,
    int? ParticipantCount,
    IReadOnlyList<TempleOsrsParticipantMetric> Participants,
    IReadOnlyList<TempleOsrsTeamMetric> Teams);

public sealed record TempleOsrsParticipantMetric(
    string RuneScapeName,
    string DisplayName,
    long? StartValue,
    long? CurrentValue,
    long GainedValue,
    int? Rank,
    string? TeamKey,
    string? TeamName,
    DateTimeOffset? LastCheckedAt,
    DateTimeOffset? LastChangedAt,
    bool? HasDatapoints,
    bool? OnHiscores);

public sealed record TempleOsrsTeamMetric(
    string TempleTeamKey,
    string TeamName,
    long? StartValue,
    long? CurrentValue,
    long GainedValue,
    int? Rank,
    string? MvpRuneScapeName,
    IReadOnlyList<string> Members);

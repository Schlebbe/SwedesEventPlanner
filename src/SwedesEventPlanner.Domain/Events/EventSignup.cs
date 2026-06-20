using SwedesEventPlanner.Domain.Common;

namespace SwedesEventPlanner.Domain.Events;

public sealed class EventSignup
{
    public long Id { get; set; }

    public long EventId { get; set; }

    public long? PlayerId { get; set; }

    public required string RuneScapeName { get; set; }

    public string? DisplayName { get; set; }

    public string? EmailHash { get; set; }

    public string? AvailabilityText { get; set; }

    public decimal? DailyHours { get; set; }

    public string? PreferredContent { get; set; }

    public string? TeamPreference { get; set; }

    public string? Notes { get; set; }

    public string Status { get; set; } = EventSignupStatuses.Imported;

    public string SourceSystem { get; set; } = EventSignupSources.GoogleForms;

    public string? SourceRowHash { get; set; }

    public DateTimeOffset? SubmittedAt { get; set; }

    public DateTimeOffset ImportedAt { get; set; }

    public string MetadataJson { get; set; } = JsonDefaults.Object;
}

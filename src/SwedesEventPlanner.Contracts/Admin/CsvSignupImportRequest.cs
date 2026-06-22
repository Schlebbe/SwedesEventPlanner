using System.ComponentModel.DataAnnotations;

namespace SwedesEventPlanner.Contracts.Admin;

/// <summary>Payload for importing event signup rows from a Google Forms-style CSV export.</summary>
public sealed record CsvSignupImportRequest
{
    /// <summary>Raw CSV text copied from the signup export.</summary>
    [Required]
    public required string CsvText { get; init; }
}

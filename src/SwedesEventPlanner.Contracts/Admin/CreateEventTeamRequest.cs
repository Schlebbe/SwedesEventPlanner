using System.ComponentModel.DataAnnotations;

namespace SwedesEventPlanner.Contracts.Admin;

/// <summary>Payload for creating an event-scoped team.</summary>
public sealed record CreateEventTeamRequest
{
    /// <summary>Team name visible in event setup and participant-facing views.</summary>
    [Required]
    public required string Name { get; init; }
}

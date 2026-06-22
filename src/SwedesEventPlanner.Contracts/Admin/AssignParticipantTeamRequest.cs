namespace SwedesEventPlanner.Contracts.Admin;

/// <summary>Payload for assigning or clearing an event participant's team.</summary>
public sealed record AssignParticipantTeamRequest
{
    /// <summary>Event team ID, or null to clear the assignment.</summary>
    public long? TeamId { get; init; }
}

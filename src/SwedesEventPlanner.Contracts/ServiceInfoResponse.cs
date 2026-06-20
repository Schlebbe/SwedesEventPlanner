namespace SwedesEventPlanner.Contracts;

/// <summary>Describes the running Swedes Event Planner API surface.</summary>
public sealed record ServiceInfoResponse(
    string Name,
    string Environment,
    string[] Routes);

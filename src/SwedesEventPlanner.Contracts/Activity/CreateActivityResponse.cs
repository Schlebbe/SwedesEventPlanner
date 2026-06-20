namespace SwedesEventPlanner.Contracts.Activity;

/// <summary>Result returned after accepting or deduplicating mock activity.</summary>
public sealed record CreateActivityResponse(
    long ActivityEventId,
    long? QueueItemId,
    bool Duplicate,
    string Status);

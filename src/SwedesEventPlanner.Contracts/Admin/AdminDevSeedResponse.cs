namespace SwedesEventPlanner.Contracts.Admin;

/// <summary>Local development seed data created for activity processing smoke tests.</summary>
public sealed record AdminDevSeedResponse(
    long PlayerId,
    long EventId,
    long TeamId,
    long PointTileId,
    long CountTileId,
    string PlayerName,
    string EventSlug,
    string[] SampleRequests);

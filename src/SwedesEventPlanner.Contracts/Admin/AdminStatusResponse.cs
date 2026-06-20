namespace SwedesEventPlanner.Contracts.Admin;

/// <summary>Represents the admin/testing surface status.</summary>
public sealed record AdminStatusResponse(string Status, string[] AvailableLater);

namespace SwedesEventPlanner.Domain.ExternalCompetitions;

public static class ExternalCompetitionSyncRunStatuses
{
    public const string Queued = "queued";
    public const string Running = "running";
    public const string Succeeded = "succeeded";
    public const string Failed = "failed";
    public const string SkippedCooldown = "skipped_cooldown";
    public const string SkippedAlreadyRunning = "skipped_already_running";
}

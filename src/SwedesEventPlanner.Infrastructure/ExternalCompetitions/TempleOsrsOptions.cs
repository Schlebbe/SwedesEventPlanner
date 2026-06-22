namespace SwedesEventPlanner.Infrastructure.ExternalCompetitions;

public sealed class TempleOsrsOptions
{
    public const string SectionName = "TempleOsrs";

    public string BaseUrl { get; set; } = "https://templeosrs.com/api/";

    public int TimeoutSeconds { get; set; } = 15;

    public string UserAgent { get; set; } = "SwedesEventPlanner/1.0 (+https://github.com/swedes-event-planner)";
}

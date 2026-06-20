using SwedesEventPlanner.Domain.Common;
using SwedesEventPlanner.Domain.ExternalCompetitions;

namespace SwedesEventPlanner.Domain.Tests.ExternalCompetitions;

public sealed class ExternalCompetitionDefaultsTests
{
    [Fact]
    public void External_competitions_default_to_active_unknown_mode_with_json_config()
    {
        var competition = new ExternalCompetition
        {
            Provider = ExternalCompetitionProviders.TempleOsrs,
            ExternalId = "123",
            Name = "Spring Bingo",
            MetricType = "xp",
            MetricKey = "overall"
        };

        Assert.Equal(ExternalCompetitionStatuses.Active, competition.Status);
        Assert.Equal(ExternalCompetitionModes.Unknown, competition.CompetitionMode);
        Assert.Equal(JsonDefaults.Object, competition.ConfigJson);
    }

    [Theory]
    [InlineData(ExternalCompetitionStatuses.Active)]
    [InlineData(ExternalCompetitionStatuses.Disabled)]
    [InlineData(ExternalCompetitionSyncRunStatuses.Queued)]
    [InlineData(ExternalCompetitionSyncRunStatuses.Running)]
    [InlineData(ExternalCompetitionSyncRunStatuses.Succeeded)]
    [InlineData(ExternalCompetitionSyncRunStatuses.Failed)]
    [InlineData(ExternalCompetitionSyncRunStatuses.SkippedCooldown)]
    [InlineData(ExternalCompetitionSyncRunStatuses.SkippedAlreadyRunning)]
    [InlineData(ExternalCompetitionPlayerReviewStatuses.Unreviewed)]
    [InlineData(ExternalCompetitionPlayerReviewStatuses.Resolved)]
    [InlineData(ExternalCompetitionPlayerReviewStatuses.Ignored)]
    public void Status_constants_are_lowercase_text_values(string status)
    {
        Assert.Equal(status, status.ToLowerInvariant());
    }
}

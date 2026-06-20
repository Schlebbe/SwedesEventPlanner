using SwedesEventPlanner.Domain.Events;

namespace SwedesEventPlanner.Domain.Tests.Events;

public sealed class EventStatusesTests
{
    [Theory]
    [InlineData(EventStatuses.Draft)]
    [InlineData(EventStatuses.Scheduled)]
    [InlineData(EventStatuses.Active)]
    [InlineData(EventStatuses.Completed)]
    [InlineData(EventStatuses.Cancelled)]
    public void Status_constants_are_lowercase_text_values(string status)
    {
        Assert.Equal(status, status.ToLowerInvariant());
    }
}

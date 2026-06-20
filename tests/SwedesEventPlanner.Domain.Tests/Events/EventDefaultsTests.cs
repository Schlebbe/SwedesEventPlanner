using SwedesEventPlanner.Domain.Events;

namespace SwedesEventPlanner.Domain.Tests.Events;

public sealed class EventDefaultsTests
{
    [Fact]
    public void TimeZone_defaults_to_stockholm()
    {
        Assert.Equal("Europe/Stockholm", EventDefaults.TimeZone);
    }
}

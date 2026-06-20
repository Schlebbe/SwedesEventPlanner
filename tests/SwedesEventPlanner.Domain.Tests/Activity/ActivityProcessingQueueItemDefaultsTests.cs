using SwedesEventPlanner.Domain.Activity;

namespace SwedesEventPlanner.Domain.Tests.Activity;

public sealed class ActivityProcessingQueueItemDefaultsTests
{
    [Fact]
    public void Queue_items_start_pending_with_no_attempts()
    {
        var item = new ActivityProcessingQueueItem();

        Assert.Equal(ActivityProcessingStatuses.Pending, item.Status);
        Assert.Equal(0, item.Attempts);
    }
}

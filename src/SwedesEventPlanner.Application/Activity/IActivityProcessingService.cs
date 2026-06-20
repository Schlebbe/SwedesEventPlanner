namespace SwedesEventPlanner.Application.Activity;

public interface IActivityProcessingService
{
    Task<int> ProcessPendingActivityAsync(
        int maxBatchSize,
        CancellationToken cancellationToken);

    Task ProcessActivityAsync(
        long activityEventId,
        CancellationToken cancellationToken);
}

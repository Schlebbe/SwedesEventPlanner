using SwedesEventPlanner.Application.Activity;

namespace SwedesEventPlanner.Worker;

public sealed class ActivityProcessingWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<ActivityProcessingWorker> logger,
    IConfiguration configuration) : BackgroundService
{
    private readonly TimeSpan idleDelay = TimeSpan.FromSeconds(
        configuration.GetValue("Worker:ActivityIdleDelaySeconds", 5));

    private readonly int batchSize = configuration.GetValue("Worker:ActivityBatchSize", 10);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Swedes Event Planner activity processing worker started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var processingService = scope.ServiceProvider.GetRequiredService<IActivityProcessingService>();
                var processedCount = await processingService.ProcessPendingActivityAsync(batchSize, stoppingToken);

                if (processedCount == 0)
                {
                    await Task.Delay(idleDelay, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Activity processing worker iteration failed.");
                await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
            }
        }
    }
}

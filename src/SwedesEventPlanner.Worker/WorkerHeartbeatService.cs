namespace SwedesEventPlanner.Worker;

public sealed class WorkerHeartbeatService(ILogger<WorkerHeartbeatService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Swedes Event Planner worker started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            logger.LogDebug("Worker heartbeat at {UtcNow}.", DateTimeOffset.UtcNow);
            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        }
    }
}

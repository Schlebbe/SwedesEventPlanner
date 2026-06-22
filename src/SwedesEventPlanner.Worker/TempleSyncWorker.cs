using SwedesEventPlanner.Application.ExternalCompetitions;

namespace SwedesEventPlanner.Worker;

public sealed class TempleSyncWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<TempleSyncWorker> logger,
    IConfiguration configuration) : BackgroundService
{
    private readonly TimeSpan idleDelay = TimeSpan.FromSeconds(
        configuration.GetValue("Worker:TempleSyncIdleDelaySeconds", 60));

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Swedes Event Planner TempleOSRS sync worker started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var syncService = scope.ServiceProvider.GetRequiredService<IExternalCompetitionSyncService>();
                var started = await syncService.SyncDueActiveCompetitionsAsync(stoppingToken);

                if (started > 0)
                {
                    logger.LogInformation("Started {Count} read-only TempleOSRS sync run(s).", started);
                }

                await Task.Delay(idleDelay, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "TempleOSRS sync worker iteration failed.");
                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
            }
        }
    }
}

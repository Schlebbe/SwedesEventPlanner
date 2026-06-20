using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace SwedesEventPlanner.Infrastructure.Persistence;

public static class DatabaseMigrationExtensions
{
    public static async Task ApplyDatabaseMigrationsAsync(
        this IServiceProvider serviceProvider,
        CancellationToken cancellationToken = default)
    {
        using var scope = serviceProvider.CreateScope();
        var logger = scope.ServiceProvider
            .GetRequiredService<ILogger<EventPlannerDbContext>>();
        var dbContext = scope.ServiceProvider.GetRequiredService<EventPlannerDbContext>();

        logger.LogInformation("Applying EF Core migrations for Event Planner database.");
        await dbContext.Database.MigrateAsync(cancellationToken);
    }
}

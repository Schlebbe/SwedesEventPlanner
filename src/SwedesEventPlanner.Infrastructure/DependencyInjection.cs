using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SwedesEventPlanner.Application.Activity;
using SwedesEventPlanner.Application.Admin;
using SwedesEventPlanner.Application.Events;
using SwedesEventPlanner.Application.ExternalCompetitions;
using SwedesEventPlanner.Infrastructure.Activity;
using SwedesEventPlanner.Infrastructure.Admin;
using SwedesEventPlanner.Infrastructure.Events;
using SwedesEventPlanner.Infrastructure.ExternalCompetitions;
using SwedesEventPlanner.Infrastructure.Persistence;

namespace SwedesEventPlanner.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "ConnectionStrings:DefaultConnection must be configured for PostgreSQL.");
        }

        services.AddDbContext<EventPlannerDbContext>(options =>
        {
            options.UseNpgsql(
                connectionString,
                npgsqlOptions => npgsqlOptions.MigrationsAssembly(typeof(EventPlannerDbContext).Assembly.FullName));
        });

        services.AddScoped<IActivityIngestionService, ActivityIngestionService>();
        services.AddScoped<IActivityProcessingService, ActivityProcessingService>();
        services.AddScoped<IAdminDevSeedService, AdminDevSeedService>();
        services.AddScoped<IAdminEventSetupService, AdminEventSetupService>();
        services.AddScoped<IExternalCompetitionSyncService, ExternalCompetitionSyncService>();
        services.AddScoped<IEventReadService, EventReadService>();
        services.Configure<TempleOsrsOptions>(configuration.GetSection(TempleOsrsOptions.SectionName));
        services.AddHttpClient<ITempleOsrsClient, TempleOsrsClient>((serviceProvider, client) =>
        {
            var options = serviceProvider
                .GetRequiredService<Microsoft.Extensions.Options.IOptions<TempleOsrsOptions>>()
                .Value;
            TempleOsrsClient.ConfigureHttpClient(client, options);
        });

        return services;
    }
}

using Serilog;
using SwedesEventPlanner.Application;
using SwedesEventPlanner.Infrastructure;

namespace SwedesEventPlanner.Worker;

public static class Program
{
    public static async Task Main(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);

        builder.Services.AddSerilog((services, loggerConfiguration) =>
        {
            loggerConfiguration
                .ReadFrom.Configuration(builder.Configuration)
                .ReadFrom.Services(services)
                .Enrich.FromLogContext()
                .WriteTo.Console();
        });

        builder.Services.AddApplication();
        builder.Services.AddInfrastructure(builder.Configuration);
        builder.Services.AddHostedService<ActivityProcessingWorker>();

        var host = builder.Build();
        await host.RunAsync();
    }
}

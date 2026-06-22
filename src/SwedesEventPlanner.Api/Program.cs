using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Serilog;
using SwedesEventPlanner.Api.Endpoints;
using SwedesEventPlanner.Application;
using SwedesEventPlanner.Infrastructure;
using SwedesEventPlanner.Infrastructure.Persistence;

namespace SwedesEventPlanner.Api;

public static class Program
{
    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.Host.UseSerilog((context, services, loggerConfiguration) =>
        {
            loggerConfiguration
                .ReadFrom.Configuration(context.Configuration)
                .ReadFrom.Services(services)
                .Enrich.FromLogContext()
                .WriteTo.Console();
        });

        builder.Services.AddApplication();
        builder.Services.AddInfrastructure(builder.Configuration);

        builder.Services.AddProblemDetails();
        builder.Services.AddOpenApi();

        builder.Services.ConfigureHttpJsonOptions(options =>
        {
            options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
        });

        builder.Services.AddCors(options =>
        {
            options.AddPolicy("LocalFrontend", policy =>
            {
                policy
                    .WithOrigins("http://localhost:5173", "https://localhost:5173")
                    .AllowAnyHeader()
                    .AllowAnyMethod();
            });
        });

        builder.Services.AddHealthChecks()
            .AddDbContextCheck<EventPlannerDbContext>(
                name: "postgresql",
                failureStatus: HealthStatus.Unhealthy,
                tags: ["ready"]);

        var app = builder.Build();

        await ConfigureAsync(app);
        await app.RunAsync();
    }

    private static async Task ConfigureAsync(WebApplication app)
    {
        app.UseExceptionHandler();
        app.UseStatusCodePages();
        app.Use(async (context, next) =>
        {
            try
            {
                await next(context);
            }
            catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
            {
                context.RequestServices
                    .GetRequiredService<ILoggerFactory>()
                    .CreateLogger("RequestCancellation")
                    .LogDebug("Request was cancelled by the client: {Path}", context.Request.Path);
            }
        });

        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi();
            app.UseCors("LocalFrontend");

            if (app.Configuration.GetValue<bool>("Database:ApplyMigrationsOnStartup"))
            {
                await app.Services.ApplyDatabaseMigrationsAsync();
            }
        }
        else
        {
            app.UseDefaultFiles();
            app.UseStaticFiles();
        }

        app.MapHealthChecks("/health", new HealthCheckOptions
        {
            Predicate = _ => false,
        });

        app.MapHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate = healthCheck => healthCheck.Tags.Contains("ready"),
        });

        app.MapServiceInfoEndpoints();
        app.MapEventEndpoints();
        app.MapAdminEndpoints();

        if (app.Environment.IsDevelopment())
        {
            app.MapActivityEndpoints();
        }

        if (!app.Environment.IsDevelopment())
        {
            app.MapFallbackToFile("index.html");
        }
    }
}

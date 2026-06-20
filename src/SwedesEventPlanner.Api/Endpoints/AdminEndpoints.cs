using Microsoft.AspNetCore.Http.HttpResults;
using SwedesEventPlanner.Application.Admin;
using SwedesEventPlanner.Contracts.Admin;

namespace SwedesEventPlanner.Api.Endpoints;

public static class AdminEndpoints
{
    public static RouteGroupBuilder MapAdminEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/admin")
            .WithTags("Admin");

        group.MapGet("/status", Ok<AdminStatusResponse> (CancellationToken cancellationToken) =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            return TypedResults.Ok(new AdminStatusResponse(
                "scaffolded",
                [
                    "CSV event signup import",
                    "team assignment",
                    "unmatched TempleOSRS identity review",
                    "sync/export diagnostics",
                ]));
        })
        .WithName("GetAdminStatus")
        .WithSummary("Get admin/testing surface status.")
        .Produces<AdminStatusResponse>(StatusCodes.Status200OK);

        group.MapPost("/dev/seed-mock-activity-demo", async Task<Results<Ok<AdminDevSeedResponse>, NotFound>> (
            IHostEnvironment environment,
            IAdminDevSeedService seedService,
            CancellationToken cancellationToken) =>
        {
            if (!environment.IsDevelopment())
            {
                return TypedResults.NotFound();
            }

            var response = await seedService.SeedMockActivityDemoAsync(cancellationToken);
            return TypedResults.Ok(response);
        })
        .WithName("SeedMockActivityDemo")
        .WithSummary("Seed local development data for mock activity processing.")
        .WithDescription("Development-only helper that creates a known player, active event, team, participant, board, tiles, and basic rules.")
        .Produces<AdminDevSeedResponse>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound);

        return group;
    }
}

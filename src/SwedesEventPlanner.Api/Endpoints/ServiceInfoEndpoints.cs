using Microsoft.AspNetCore.Http.HttpResults;
using SwedesEventPlanner.Contracts;

namespace SwedesEventPlanner.Api.Endpoints;

public static class ServiceInfoEndpoints
{
    public static RouteGroupBuilder MapServiceInfoEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api")
            .WithTags("Service");

        group.MapGet("/", Ok<ServiceInfoResponse> (
            IWebHostEnvironment environment,
            CancellationToken cancellationToken) =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            var endpoints = new List<string>
            {
                "/api/events",
                "/api/admin/status",
            };

            if (environment.IsDevelopment())
            {
                endpoints.Add("/api/activity");
            }

            return TypedResults.Ok(new ServiceInfoResponse(
                "Swedes Event Planner",
                environment.EnvironmentName,
                endpoints.ToArray()));
        })
        .WithName("GetServiceInfo")
        .WithSummary("Get API service information.")
        .Produces<ServiceInfoResponse>(StatusCodes.Status200OK);

        return group;
    }
}

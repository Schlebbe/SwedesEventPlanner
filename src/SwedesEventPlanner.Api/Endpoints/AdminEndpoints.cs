using Microsoft.AspNetCore.Http.HttpResults;
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

        return group;
    }
}

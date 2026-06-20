using Microsoft.AspNetCore.Http.HttpResults;

namespace SwedesEventPlanner.Api.Endpoints;

public static class ActivityEndpoints
{
    public static RouteGroupBuilder MapActivityEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/activity")
            .WithTags("Activity");

        group.MapPost("/", ProblemHttpResult (
            CancellationToken cancellationToken) =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            return TypedResults.Problem(
                title: "Activity ingestion is not implemented yet.",
                detail: "The mock/dev activity route is reserved for the next backend pass.",
                statusCode: StatusCodes.Status501NotImplemented);
        })
        .WithName("CreateActivity")
        .WithSummary("Reserve the mock/dev activity ingestion route.")
        .ProducesProblem(StatusCodes.Status501NotImplemented);

        return group;
    }
}

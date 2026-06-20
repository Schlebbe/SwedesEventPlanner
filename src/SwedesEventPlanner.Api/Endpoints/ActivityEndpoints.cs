using Microsoft.AspNetCore.Http.HttpResults;
using SwedesEventPlanner.Application.Activity;
using SwedesEventPlanner.Contracts.Activity;

namespace SwedesEventPlanner.Api.Endpoints;

public static class ActivityEndpoints
{
    public static RouteGroupBuilder MapActivityEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/activity")
            .WithTags("Activity");

        group.MapPost("/", async Task<Results<Created<CreateActivityResponse>, ProblemHttpResult>> (
            CreateActivityRequest request,
            IActivityIngestionService activityIngestionService,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var response = await activityIngestionService.CreateActivityAsync(request, cancellationToken);
                return TypedResults.Created($"/api/activity/{response.ActivityEventId}", response);
            }
            catch (ActivityIngestionException exception)
            {
                return TypedResults.Problem(
                    title: "Invalid activity payload.",
                    detail: exception.Message,
                    statusCode: StatusCodes.Status400BadRequest);
            }
        })
        .WithName("CreateActivity")
        .WithSummary("Ingest mock/dev activity.")
        .WithDescription("Stores a mock activity event and queues it for asynchronous event processing.")
        .Produces<CreateActivityResponse>(StatusCodes.Status201Created)
        .ProducesProblem(StatusCodes.Status400BadRequest);

        return group;
    }
}

using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using SwedesEventPlanner.Contracts.Events;
using SwedesEventPlanner.Infrastructure.Persistence;

namespace SwedesEventPlanner.Api.Endpoints;

public static class EventEndpoints
{
    public static RouteGroupBuilder MapEventEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/events")
            .WithTags("Events");

        group.MapGet("/", async Task<Ok<EventListResponse>> (
            EventPlannerDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            var events = await dbContext.Events
                .AsNoTracking()
                .OrderBy(eventDefinition => eventDefinition.StartsAt)
                .Select(eventDefinition => new EventSummaryResponse(
                    eventDefinition.Id,
                    eventDefinition.Slug,
                    eventDefinition.Name,
                    eventDefinition.EventType,
                    eventDefinition.Status,
                    eventDefinition.StartsAt,
                    eventDefinition.EndsAt,
                    eventDefinition.TimeZone))
                .ToListAsync(cancellationToken);

            return TypedResults.Ok(new EventListResponse(events));
        })
        .WithName("ListEvents")
        .WithSummary("List public events.")
        .Produces<EventListResponse>(StatusCodes.Status200OK);

        return group;
    }
}

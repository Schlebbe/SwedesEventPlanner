using Microsoft.AspNetCore.Http.HttpResults;
using SwedesEventPlanner.Application.Events;
using SwedesEventPlanner.Application.ExternalCompetitions;
using SwedesEventPlanner.Contracts.Events;

namespace SwedesEventPlanner.Api.Endpoints;

public static class EventEndpoints
{
    public static RouteGroupBuilder MapEventEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/events")
            .WithTags("Events");

        group.MapGet("/", async Task<Ok<EventListResponse>> (
            IEventReadService eventReadService,
            CancellationToken cancellationToken) =>
        {
            var events = await eventReadService.ListEventsAsync(cancellationToken);
            return TypedResults.Ok(events);
        })
        .WithName("ListEvents")
        .WithSummary("List public events.")
        .Produces<EventListResponse>(StatusCodes.Status200OK);

        group.MapGet("/{slug}", async Task<Results<Ok<EventSummaryResponse>, NotFound>> (
            string slug,
            IEventReadService eventReadService,
            CancellationToken cancellationToken) =>
        {
            var eventSummary = await eventReadService.GetEventAsync(slug, cancellationToken);
            return eventSummary is null
                ? TypedResults.NotFound()
                : TypedResults.Ok(eventSummary);
        })
        .WithName("GetEventBySlug")
        .WithSummary("Get a public event by slug.")
        .Produces<EventSummaryResponse>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound);

        group.MapGet("/{slug}/board", async Task<Results<Ok<EventBoardResponse>, NotFound>> (
            string slug,
            IEventReadService eventReadService,
            CancellationToken cancellationToken) =>
        {
            var board = await eventReadService.GetBoardAsync(slug, cancellationToken);
            return board is null
                ? TypedResults.NotFound()
                : TypedResults.Ok(board);
        })
        .WithName("GetEventBoard")
        .WithSummary("Get a public event board with team progress.")
        .Produces<EventBoardResponse>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound);

        group.MapGet("/{slug}/teams", async Task<Results<Ok<EventTeamListResponse>, NotFound>> (
            string slug,
            IEventReadService eventReadService,
            CancellationToken cancellationToken) =>
        {
            var teams = await eventReadService.GetTeamsAsync(slug, cancellationToken);
            return teams is null
                ? TypedResults.NotFound()
                : TypedResults.Ok(teams);
        })
        .WithName("GetEventTeams")
        .WithSummary("Get public event team summaries.")
        .Produces<EventTeamListResponse>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound);

        group.MapGet("/{slug}/contributions", async Task<Results<Ok<EventContributionListResponse>, NotFound>> (
            string slug,
            IEventReadService eventReadService,
            CancellationToken cancellationToken,
            int limit = 25) =>
        {
            var contributions = await eventReadService.GetContributionsAsync(slug, limit, cancellationToken);
            return contributions is null
                ? TypedResults.NotFound()
                : TypedResults.Ok(contributions);
        })
        .WithName("GetEventContributions")
        .WithSummary("Get recent public event progress contributions.")
        .Produces<EventContributionListResponse>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound);

        group.MapPost("/{slug}/templeosrs/refresh", async Task<Results<Ok<EventTempleRefreshResponse>, NotFound>> (
            string slug,
            IExternalCompetitionSyncService syncService,
            CancellationToken cancellationToken) =>
        {
            var response = await syncService.RequestPublicRefreshAsync(slug, cancellationToken);
            return response is null
                ? TypedResults.NotFound()
                : TypedResults.Ok(response);
        })
        .WithName("RequestEventTempleOsrsRefresh")
        .WithSummary("Request a public read-only TempleOSRS refresh for an event.")
        .Produces<EventTempleRefreshResponse>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound);

        return group;
    }
}

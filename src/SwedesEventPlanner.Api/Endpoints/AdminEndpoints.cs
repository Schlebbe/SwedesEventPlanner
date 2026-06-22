using Microsoft.AspNetCore.Http.HttpResults;
using SwedesEventPlanner.Application.Admin;
using SwedesEventPlanner.Application.ExternalCompetitions;
using SwedesEventPlanner.Contracts.Admin;

namespace SwedesEventPlanner.Api.Endpoints;

public static class AdminEndpoints
{
    public static RouteGroupBuilder MapAdminEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/admin")
            .WithTags("Admin")
            .AddEndpointFilter<AdminTokenEndpointFilter>();

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

        group.MapPost("/events/{eventSlug}/signups/import-csv", async Task<Results<Ok<CsvSignupImportResponse>, NotFound, ProblemHttpResult>> (
            string eventSlug,
            CsvSignupImportRequest request,
            IAdminEventSetupService setupService,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var response = await setupService.ImportCsvSignupsAsync(eventSlug, request, cancellationToken);
                return TypedResults.Ok(response);
            }
            catch (AdminEventSetupNotFoundException)
            {
                return TypedResults.NotFound();
            }
            catch (AdminEventSetupException exception)
            {
                return TypedResults.Problem(
                    title: exception.Message,
                    statusCode: StatusCodes.Status400BadRequest);
            }
        })
        .WithName("ImportEventSignupsCsv")
        .WithSummary("Import Google Forms-style CSV signups for an event.")
        .WithDescription("Admin/testing endpoint that creates or updates event signups, local players, and event participants from pasted CSV text.")
        .Produces<CsvSignupImportResponse>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status400BadRequest);

        group.MapGet("/events/{eventSlug}/signups", async Task<Results<Ok<AdminEventSignupListResponse>, NotFound>> (
            string eventSlug,
            IAdminEventSetupService setupService,
            CancellationToken cancellationToken) =>
        {
            var response = await setupService.ListSignupsAsync(eventSlug, cancellationToken);
            return response is null ? TypedResults.NotFound() : TypedResults.Ok(response);
        })
        .WithName("GetAdminEventSignups")
        .WithSummary("List imported event signups for admin setup.")
        .Produces<AdminEventSignupListResponse>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound);

        group.MapGet("/events/{eventSlug}/participants", async Task<Results<Ok<AdminEventParticipantListResponse>, NotFound>> (
            string eventSlug,
            IAdminEventSetupService setupService,
            CancellationToken cancellationToken) =>
        {
            var response = await setupService.ListParticipantsAsync(eventSlug, cancellationToken);
            return response is null ? TypedResults.NotFound() : TypedResults.Ok(response);
        })
        .WithName("GetAdminEventParticipants")
        .WithSummary("List event participants and team assignments for admin setup.")
        .Produces<AdminEventParticipantListResponse>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound);

        group.MapPost("/events/{eventSlug}/teams", async Task<Results<Created<AdminEventTeamResponse>, NotFound, ProblemHttpResult>> (
            string eventSlug,
            CreateEventTeamRequest request,
            IAdminEventSetupService setupService,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var response = await setupService.CreateTeamAsync(eventSlug, request, cancellationToken);
                return response is null
                    ? TypedResults.NotFound()
                    : TypedResults.Created($"/api/admin/events/{eventSlug}/teams/{response.Id}", response);
            }
            catch (AdminEventSetupException exception)
            {
                return TypedResults.Problem(
                    title: exception.Message,
                    statusCode: StatusCodes.Status400BadRequest);
            }
        })
        .WithName("CreateAdminEventTeam")
        .WithSummary("Create an event team for manual assignment.")
        .Produces<AdminEventTeamResponse>(StatusCodes.Status201Created)
        .Produces(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status400BadRequest);

        group.MapPost("/events/{eventSlug}/participants/{participantId:long}/assign-team", async Task<Results<Ok<AdminEventParticipantResponse>, NotFound, ProblemHttpResult>> (
            string eventSlug,
            long participantId,
            AssignParticipantTeamRequest request,
            IAdminEventSetupService setupService,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var response = await setupService.AssignParticipantTeamAsync(eventSlug, participantId, request, cancellationToken);
                return response is null ? TypedResults.NotFound() : TypedResults.Ok(response);
            }
            catch (AdminEventSetupException exception)
            {
                return TypedResults.Problem(
                    title: exception.Message,
                    statusCode: StatusCodes.Status400BadRequest);
            }
        })
        .WithName("AssignAdminEventParticipantTeam")
        .WithSummary("Assign or clear an event participant's team.")
        .Produces<AdminEventParticipantResponse>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status400BadRequest);

        group.MapPost("/events/{eventSlug}/external-competitions/templeosrs", async Task<Results<Created<AdminExternalCompetitionResponse>, NotFound, ProblemHttpResult>> (
            string eventSlug,
            LinkExternalCompetitionRequest request,
            IExternalCompetitionSyncService syncService,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var response = await syncService.LinkTempleCompetitionAsync(eventSlug, request, cancellationToken);
                return response is null
                    ? TypedResults.NotFound()
                    : TypedResults.Created($"/api/admin/events/{eventSlug}/external-competitions/{response.Id}", response);
            }
            catch (AdminEventSetupException exception)
            {
                return TypedResults.Problem(
                    title: exception.Message,
                    statusCode: StatusCodes.Status400BadRequest);
            }
        })
        .WithName("LinkTempleOsrsCompetition")
        .WithSummary("Link a read-only TempleOSRS competition to an event.")
        .Produces<AdminExternalCompetitionResponse>(StatusCodes.Status201Created)
        .Produces(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status400BadRequest);

        group.MapGet("/events/{eventSlug}/external-competitions", async Task<Results<Ok<AdminExternalCompetitionListResponse>, NotFound>> (
            string eventSlug,
            IExternalCompetitionSyncService syncService,
            CancellationToken cancellationToken) =>
        {
            var response = await syncService.ListCompetitionsAsync(eventSlug, cancellationToken);
            return response is null ? TypedResults.NotFound() : TypedResults.Ok(response);
        })
        .WithName("ListAdminExternalCompetitions")
        .WithSummary("List external competitions linked to an event.")
        .Produces<AdminExternalCompetitionListResponse>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound);

        group.MapPost("/events/{eventSlug}/external-competitions/{externalCompetitionId:long}/sync", async Task<Results<Ok<AdminExternalCompetitionSyncRunResponse>, NotFound>> (
            string eventSlug,
            long externalCompetitionId,
            IExternalCompetitionSyncService syncService,
            CancellationToken cancellationToken) =>
        {
            var response = await syncService.SyncCompetitionAsync(eventSlug, externalCompetitionId, cancellationToken);
            return response is null ? TypedResults.NotFound() : TypedResults.Ok(response);
        })
        .WithName("SyncAdminExternalCompetition")
        .WithSummary("Trigger a read-only sync for a linked external competition.")
        .Produces<AdminExternalCompetitionSyncRunResponse>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound);

        group.MapGet("/external-competitions/{externalCompetitionId:long}/sync-runs", async Task<Ok<AdminExternalCompetitionSyncRunListResponse>> (
            long externalCompetitionId,
            IExternalCompetitionSyncService syncService,
            CancellationToken cancellationToken) =>
        {
            var response = await syncService.ListSyncRunsAsync(externalCompetitionId, cancellationToken);
            return TypedResults.Ok(response);
        })
        .WithName("ListAdminExternalCompetitionSyncRuns")
        .WithSummary("List recent sync runs for an external competition.")
        .Produces<AdminExternalCompetitionSyncRunListResponse>(StatusCodes.Status200OK);

        group.MapGet("/external-competitions/{externalCompetitionId:long}/player-metrics", async Task<Ok<AdminExternalCompetitionPlayerMetricListResponse>> (
            long externalCompetitionId,
            IExternalCompetitionSyncService syncService,
            CancellationToken cancellationToken) =>
        {
            var response = await syncService.ListPlayerMetricsAsync(externalCompetitionId, cancellationToken);
            return TypedResults.Ok(response);
        })
        .WithName("ListAdminExternalCompetitionPlayerMetrics")
        .WithSummary("List cached player metrics for an external competition.")
        .Produces<AdminExternalCompetitionPlayerMetricListResponse>(StatusCodes.Status200OK);

        group.MapGet("/external-competitions/{externalCompetitionId:long}/team-metrics", async Task<Ok<AdminExternalCompetitionTeamMetricListResponse>> (
            long externalCompetitionId,
            IExternalCompetitionSyncService syncService,
            CancellationToken cancellationToken) =>
        {
            var response = await syncService.ListTeamMetricsAsync(externalCompetitionId, cancellationToken);
            return TypedResults.Ok(response);
        })
        .WithName("ListAdminExternalCompetitionTeamMetrics")
        .WithSummary("List cached team metrics for an external competition.")
        .Produces<AdminExternalCompetitionTeamMetricListResponse>(StatusCodes.Status200OK);

        group.MapGet("/external-competitions/{externalCompetitionId:long}/unmatched-identities", async Task<Ok<AdminExternalCompetitionUnmatchedIdentityListResponse>> (
            long externalCompetitionId,
            IExternalCompetitionSyncService syncService,
            CancellationToken cancellationToken) =>
        {
            var response = await syncService.ListUnmatchedIdentitiesAsync(externalCompetitionId, cancellationToken);
            return TypedResults.Ok(response);
        })
        .WithName("ListAdminExternalCompetitionUnmatchedIdentities")
        .WithSummary("List unmatched TempleOSRS names for an external competition.")
        .Produces<AdminExternalCompetitionUnmatchedIdentityListResponse>(StatusCodes.Status200OK);

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

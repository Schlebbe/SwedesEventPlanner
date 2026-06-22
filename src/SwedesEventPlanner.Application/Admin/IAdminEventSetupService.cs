using SwedesEventPlanner.Contracts.Admin;

namespace SwedesEventPlanner.Application.Admin;

public interface IAdminEventSetupService
{
    Task<CsvSignupImportResponse> ImportCsvSignupsAsync(
        string eventSlug,
        CsvSignupImportRequest request,
        CancellationToken cancellationToken);

    Task<AdminEventSignupListResponse?> ListSignupsAsync(
        string eventSlug,
        CancellationToken cancellationToken);

    Task<AdminEventParticipantListResponse?> ListParticipantsAsync(
        string eventSlug,
        CancellationToken cancellationToken);

    Task<AdminEventTeamResponse?> CreateTeamAsync(
        string eventSlug,
        CreateEventTeamRequest request,
        CancellationToken cancellationToken);

    Task<AdminEventParticipantResponse?> AssignParticipantTeamAsync(
        string eventSlug,
        long participantId,
        AssignParticipantTeamRequest request,
        CancellationToken cancellationToken);
}

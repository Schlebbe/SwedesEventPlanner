using SwedesEventPlanner.Contracts.Admin;

namespace SwedesEventPlanner.Application.Admin;

public interface IAdminEventSetupService
{
    Task<AdminEventListResponse> ListEventsAsync(CancellationToken cancellationToken);

    Task<AdminEventSetupSummaryResponse> CreateEventAsync(
        CreateAdminEventRequest request,
        CancellationToken cancellationToken);

    Task<AdminEventSetupSummaryResponse?> UpdateEventAsync(
        string eventSlug,
        UpdateAdminEventRequest request,
        CancellationToken cancellationToken);

    Task<AdminEventSetupSummaryResponse?> SetEventStatusAsync(
        string eventSlug,
        UpdateAdminEventStatusRequest request,
        CancellationToken cancellationToken);

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

    Task<AdminBoardSetupResponse?> GetBoardSetupAsync(
        string eventSlug,
        CancellationToken cancellationToken);

    Task<AdminBingoBoardResponse?> CreateBoardAsync(
        string eventSlug,
        CreateBingoBoardRequest request,
        CancellationToken cancellationToken);

    Task<AdminBingoTileResponse?> CreateTileAsync(
        string eventSlug,
        long boardId,
        CreateBingoTileRequest request,
        CancellationToken cancellationToken);

    Task<AdminBingoTileTierResponse?> CreateTileTierAsync(
        string eventSlug,
        long tileId,
        CreateBingoTileTierRequest request,
        CancellationToken cancellationToken);

    Task<AdminTileRuleResponse?> CreateTileRuleAsync(
        string eventSlug,
        long tileId,
        UpsertTileRuleRequest request,
        CancellationToken cancellationToken);

    Task<AdminTileRuleResponse?> UpdateTileRuleAsync(
        string eventSlug,
        long tileId,
        long ruleId,
        UpsertTileRuleRequest request,
        CancellationToken cancellationToken);
}

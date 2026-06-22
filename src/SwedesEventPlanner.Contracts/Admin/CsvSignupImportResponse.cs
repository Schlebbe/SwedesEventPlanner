namespace SwedesEventPlanner.Contracts.Admin;

/// <summary>Summary of an event signup CSV import.</summary>
public sealed record CsvSignupImportResponse(
    string EventSlug,
    int RowsRead,
    int SignupsCreated,
    int SignupsUpdated,
    int PlayersCreated,
    int ParticipantsCreated,
    int ParticipantsUpdated,
    int InvalidRows,
    IReadOnlyList<CsvSignupImportRowIssueResponse> Issues);

/// <summary>Represents one CSV row that could not be imported.</summary>
public sealed record CsvSignupImportRowIssueResponse(
    int RowNumber,
    string Reason);

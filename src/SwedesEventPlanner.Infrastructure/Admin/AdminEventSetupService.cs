using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SwedesEventPlanner.Application.Admin;
using SwedesEventPlanner.Contracts.Admin;
using SwedesEventPlanner.Domain.Common;
using SwedesEventPlanner.Domain.Events;
using SwedesEventPlanner.Domain.Players;
using SwedesEventPlanner.Infrastructure.Persistence;

namespace SwedesEventPlanner.Infrastructure.Admin;

public sealed class AdminEventSetupService(EventPlannerDbContext dbContext) : IAdminEventSetupService
{
    private const string SourceSystem = EventSignupSources.GoogleForms;

    public async Task<CsvSignupImportResponse> ImportCsvSignupsAsync(
        string eventSlug,
        CsvSignupImportRequest request,
        CancellationToken cancellationToken)
    {
        var eventDefinition = await FindEventAsync(eventSlug, cancellationToken)
            ?? throw new AdminEventSetupNotFoundException("Event was not found.");

        var csvRows = ParseCsvRows(request.CsvText);
        var import = new ImportCounters(eventDefinition.Slug);
        var seenSourceHashes = new HashSet<string>(StringComparer.Ordinal);

        foreach (var row in csvRows)
        {
            import.RowsRead++;

            var runeScapeName = Pick(row.Values, "runescapename", "rsn", "ign", "ingamename", "username", "playername", "osrsname");
            if (string.IsNullOrWhiteSpace(runeScapeName))
            {
                import.AddIssue(row.RowNumber, "Missing RuneScape name.");
                continue;
            }

            runeScapeName = runeScapeName.Trim();
            var sourceRowHash = ComputeHash(string.Join('\u001f', row.OrderedValues.Select(value => value.Trim())));
            if (!seenSourceHashes.Add(sourceRowHash))
            {
                continue;
            }

            var player = await FindPlayerByRuneScapeNameAsync(runeScapeName, cancellationToken);
            if (player is null)
            {
                player = new Player
                {
                    RuneScapeName = runeScapeName,
                    DisplayName = runeScapeName,
                    CreatedAt = DateTimeOffset.UtcNow,
                };

                dbContext.Players.Add(player);
                import.PlayersCreated++;
            }

            var submittedAt = ParseDateTimeOffset(Pick(row.Values, "timestamp", "submittedat", "submitdate", "submissiontime"));
            var signup = await dbContext.EventSignups.FirstOrDefaultAsync(
                candidate => candidate.EventId == eventDefinition.Id
                    && candidate.SourceSystem == SourceSystem
                    && candidate.SourceRowHash == sourceRowHash,
                cancellationToken);

            var isNewSignup = signup is null;
            signup ??= new EventSignup
            {
                EventId = eventDefinition.Id,
                RuneScapeName = runeScapeName,
                SourceSystem = SourceSystem,
                SourceRowHash = sourceRowHash,
            };

            signup.PlayerId = player.Id == 0 ? null : player.Id;
            signup.RuneScapeName = runeScapeName;
            signup.DisplayName = Pick(row.Values, "displayname", "name");
            signup.EmailHash = HashIfPresent(Pick(row.Values, "email", "emailaddress", "e-mail", "mail"));
            signup.AvailabilityText = Pick(row.Values, "availability", "available");
            signup.DailyHours = ParseDecimal(Pick(row.Values, "dailyhours", "hoursperday", "hours", "playtime"));
            signup.PreferredContent = Pick(row.Values, "preferredcontent", "content", "preferredactivity");
            signup.TeamPreference = Pick(row.Values, "teampreference", "preferredteam", "team");
            signup.Notes = Pick(row.Values, "notes", "comments", "comment");
            signup.Status = EventSignupStatuses.Imported;
            signup.SubmittedAt = submittedAt;
            signup.ImportedAt = DateTimeOffset.UtcNow;
            signup.MetadataJson = JsonSerializer.Serialize(new CsvSignupMetadata(row.RowNumber, row.HeaderNames));

            if (isNewSignup)
            {
                dbContext.EventSignups.Add(signup);
                import.SignupsCreated++;
            }
            else
            {
                import.SignupsUpdated++;
            }

            if (player.Id == 0)
            {
                await dbContext.SaveChangesAsync(cancellationToken);
                signup.PlayerId = player.Id;
            }

            var participant = await dbContext.EventParticipants.FirstOrDefaultAsync(
                candidate => candidate.EventId == eventDefinition.Id && candidate.PlayerId == player.Id,
                cancellationToken);

            if (participant is null)
            {
                dbContext.EventParticipants.Add(new EventParticipant
                {
                    EventId = eventDefinition.Id,
                    PlayerId = player.Id,
                    JoinedAt = submittedAt ?? DateTimeOffset.UtcNow,
                    Status = EventParticipantStatuses.Active,
                    ConfigJson = JsonDefaults.Object,
                });
                import.ParticipantsCreated++;
            }
            else
            {
                participant.Status = EventParticipantStatuses.Active;
                import.ParticipantsUpdated++;
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return import.ToResponse();
    }

    public async Task<AdminEventSignupListResponse?> ListSignupsAsync(
        string eventSlug,
        CancellationToken cancellationToken)
    {
        var eventDefinition = await FindEventAsync(eventSlug, cancellationToken);
        if (eventDefinition is null)
        {
            return null;
        }

        var signups = await dbContext.EventSignups
            .AsNoTracking()
            .Where(signup => signup.EventId == eventDefinition.Id)
            .OrderBy(signup => signup.ImportedAt)
            .ThenBy(signup => signup.Id)
            .Select(signup => new AdminEventSignupResponse(
                signup.Id,
                signup.PlayerId,
                signup.RuneScapeName,
                signup.DisplayName,
                signup.AvailabilityText,
                signup.DailyHours,
                signup.PreferredContent,
                signup.TeamPreference,
                signup.Notes,
                signup.Status,
                signup.SourceSystem,
                signup.SubmittedAt,
                signup.ImportedAt))
            .ToListAsync(cancellationToken);

        return new AdminEventSignupListResponse(MapEvent(eventDefinition), signups);
    }

    public async Task<AdminEventParticipantListResponse?> ListParticipantsAsync(
        string eventSlug,
        CancellationToken cancellationToken)
    {
        var eventDefinition = await FindEventAsync(eventSlug, cancellationToken);
        if (eventDefinition is null)
        {
            return null;
        }

        var teams = await GetTeamResponsesAsync(eventDefinition.Id, cancellationToken);

        var participants = await (
            from participant in dbContext.EventParticipants.AsNoTracking()
            join player in dbContext.Players.AsNoTracking() on participant.PlayerId equals player.Id
            join team in dbContext.EventTeams.AsNoTracking() on participant.TeamId equals team.Id into teamGroup
            from team in teamGroup.DefaultIfEmpty()
            where participant.EventId == eventDefinition.Id
            orderby participant.TeamId == null descending, player.DisplayName
            select new AdminEventParticipantResponse(
                participant.Id,
                player.Id,
                player.DisplayName,
                player.RuneScapeName,
                participant.TeamId,
                team == null ? null : team.Name,
                participant.Status,
                participant.JoinedAt,
                participant.TeamId == null))
            .ToListAsync(cancellationToken);

        return new AdminEventParticipantListResponse(
            MapEvent(eventDefinition),
            teams,
            participants,
            participants.Count(participant => participant.IsUnassigned));
    }

    public async Task<AdminEventTeamResponse?> CreateTeamAsync(
        string eventSlug,
        CreateEventTeamRequest request,
        CancellationToken cancellationToken)
    {
        var eventDefinition = await FindEventAsync(eventSlug, cancellationToken);
        if (eventDefinition is null)
        {
            return null;
        }

        var name = request.Name.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new AdminEventSetupException("Team name is required.");
        }

        var existing = await dbContext.EventTeams.FirstOrDefaultAsync(
            team => team.EventId == eventDefinition.Id && team.Name == name,
            cancellationToken);

        if (existing is null)
        {
            existing = new EventTeam
            {
                EventId = eventDefinition.Id,
                Name = name,
                CreatedAt = DateTimeOffset.UtcNow,
                ConfigJson = JsonDefaults.Object,
            };

            dbContext.EventTeams.Add(existing);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        var participantCount = await dbContext.EventParticipants.CountAsync(
            participant => participant.EventId == eventDefinition.Id && participant.TeamId == existing.Id,
            cancellationToken);

        return new AdminEventTeamResponse(existing.Id, existing.Name, participantCount);
    }

    public async Task<AdminEventParticipantResponse?> AssignParticipantTeamAsync(
        string eventSlug,
        long participantId,
        AssignParticipantTeamRequest request,
        CancellationToken cancellationToken)
    {
        var eventDefinition = await FindEventAsync(eventSlug, cancellationToken);
        if (eventDefinition is null)
        {
            return null;
        }

        var participant = await dbContext.EventParticipants.FirstOrDefaultAsync(
            candidate => candidate.EventId == eventDefinition.Id && candidate.Id == participantId,
            cancellationToken);

        if (participant is null)
        {
            return null;
        }

        if (request.TeamId is not null)
        {
            var teamExists = await dbContext.EventTeams.AnyAsync(
                team => team.EventId == eventDefinition.Id && team.Id == request.TeamId,
                cancellationToken);

            if (!teamExists)
            {
                throw new AdminEventSetupException("Team was not found for this event.");
            }
        }

        participant.TeamId = request.TeamId;
        await dbContext.SaveChangesAsync(cancellationToken);

        var teamName = request.TeamId is null
            ? null
            : await dbContext.EventTeams
                .Where(team => team.Id == request.TeamId)
                .Select(team => team.Name)
                .FirstAsync(cancellationToken);

        var player = await dbContext.Players
            .AsNoTracking()
            .FirstAsync(candidate => candidate.Id == participant.PlayerId, cancellationToken);

        return new AdminEventParticipantResponse(
            participant.Id,
            player.Id,
            player.DisplayName,
            player.RuneScapeName,
            participant.TeamId,
            teamName,
            participant.Status,
            participant.JoinedAt,
            participant.TeamId == null);
    }

    private async Task<EventDefinition?> FindEventAsync(string eventSlug, CancellationToken cancellationToken)
    {
        return await dbContext.Events.FirstOrDefaultAsync(
            eventDefinition => eventDefinition.Slug == eventSlug,
            cancellationToken);
    }

    private async Task<Player?> FindPlayerByRuneScapeNameAsync(
        string runeScapeName,
        CancellationToken cancellationToken)
    {
        var normalizedName = runeScapeName.ToLower();

        return await dbContext.Players.FirstOrDefaultAsync(
            player => player.RuneScapeName.ToLower() == normalizedName,
            cancellationToken);
    }

    private async Task<List<AdminEventTeamResponse>> GetTeamResponsesAsync(
        long eventId,
        CancellationToken cancellationToken)
    {
        return await (
            from team in dbContext.EventTeams.AsNoTracking()
            where team.EventId == eventId
            orderby team.Name
            select new AdminEventTeamResponse(
                team.Id,
                team.Name,
                dbContext.EventParticipants.Count(participant => participant.TeamId == team.Id)))
            .ToListAsync(cancellationToken);
    }

    private static AdminEventSetupSummaryResponse MapEvent(EventDefinition eventDefinition)
    {
        return new AdminEventSetupSummaryResponse(
            eventDefinition.Id,
            eventDefinition.Slug,
            eventDefinition.Name,
            eventDefinition.Status,
            eventDefinition.EventType,
            eventDefinition.StartsAt,
            eventDefinition.EndsAt,
            eventDefinition.TimeZone);
    }

    private static IReadOnlyList<CsvRow> ParseCsvRows(string csvText)
    {
        if (string.IsNullOrWhiteSpace(csvText))
        {
            throw new AdminEventSetupException("CSV text is required.");
        }

        var parsedRows = ParseCsv(csvText);
        if (parsedRows.Count == 0)
        {
            throw new AdminEventSetupException("CSV did not contain a header row.");
        }

        var headers = parsedRows[0];
        var rows = new List<CsvRow>();

        for (var rowIndex = 1; rowIndex < parsedRows.Count; rowIndex++)
        {
            var values = parsedRows[rowIndex];
            if (values.All(string.IsNullOrWhiteSpace))
            {
                continue;
            }

            var mapped = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (var columnIndex = 0; columnIndex < headers.Count; columnIndex++)
            {
                var key = NormalizeHeader(headers[columnIndex]);
                if (key.Length == 0)
                {
                    continue;
                }

                mapped[key] = columnIndex < values.Count ? values[columnIndex] : string.Empty;
            }

            rows.Add(new CsvRow(rowIndex + 1, mapped, values, headers));
        }

        return rows;
    }

    private static List<List<string>> ParseCsv(string csvText)
    {
        var rows = new List<List<string>>();
        var row = new List<string>();
        var field = new StringBuilder();
        var inQuotes = false;

        for (var index = 0; index < csvText.Length; index++)
        {
            var current = csvText[index];

            if (current == '"')
            {
                if (inQuotes && index + 1 < csvText.Length && csvText[index + 1] == '"')
                {
                    field.Append('"');
                    index++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }

                continue;
            }

            if (!inQuotes && current == ',')
            {
                row.Add(field.ToString());
                field.Clear();
                continue;
            }

            if (!inQuotes && (current == '\r' || current == '\n'))
            {
                if (current == '\r' && index + 1 < csvText.Length && csvText[index + 1] == '\n')
                {
                    index++;
                }

                row.Add(field.ToString());
                field.Clear();
                rows.Add(row);
                row = [];
                continue;
            }

            field.Append(current);
        }

        row.Add(field.ToString());
        rows.Add(row);

        return rows;
    }

    private static string NormalizeHeader(string header)
    {
        var builder = new StringBuilder(header.Length);
        foreach (var character in header)
        {
            if (char.IsLetterOrDigit(character))
            {
                builder.Append(char.ToLowerInvariant(character));
            }
        }

        return builder.ToString();
    }

    private static string? Pick(IReadOnlyDictionary<string, string> values, params string[] keys)
    {
        foreach (var key in keys.Select(NormalizeHeader))
        {
            if (values.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }
        }

        return null;
    }

    private static decimal? ParseDecimal(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var result)
            ? result
            : null;
    }

    private static DateTimeOffset? ParseDateTimeOffset(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return DateTimeOffset.TryParse(
            value,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out var result)
            ? result
            : null;
    }

    private static string? HashIfPresent(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : ComputeHash(value.Trim().ToLowerInvariant());
    }

    private static string ComputeHash(string value)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    }

    private sealed record CsvRow(
        int RowNumber,
        IReadOnlyDictionary<string, string> Values,
        IReadOnlyList<string> OrderedValues,
        IReadOnlyList<string> HeaderNames);

    private sealed record CsvSignupMetadata(int RowNumber, IReadOnlyList<string> HeaderNames);

    private sealed class ImportCounters(string eventSlug)
    {
        private readonly List<CsvSignupImportRowIssueResponse> _issues = [];

        public int RowsRead { get; set; }
        public int SignupsCreated { get; set; }
        public int SignupsUpdated { get; set; }
        public int PlayersCreated { get; set; }
        public int ParticipantsCreated { get; set; }
        public int ParticipantsUpdated { get; set; }

        public void AddIssue(int rowNumber, string reason)
        {
            _issues.Add(new CsvSignupImportRowIssueResponse(rowNumber, reason));
        }

        public CsvSignupImportResponse ToResponse()
        {
            return new CsvSignupImportResponse(
                eventSlug,
                RowsRead,
                SignupsCreated,
                SignupsUpdated,
                PlayersCreated,
                ParticipantsCreated,
                ParticipantsUpdated,
                _issues.Count,
                _issues);
        }
    }
}

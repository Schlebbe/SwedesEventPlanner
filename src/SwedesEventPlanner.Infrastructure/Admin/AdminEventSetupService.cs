using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SwedesEventPlanner.Application.Admin;
using SwedesEventPlanner.Contracts.Admin;
using SwedesEventPlanner.Domain.Bingo;
using SwedesEventPlanner.Domain.Common;
using SwedesEventPlanner.Domain.Events;
using SwedesEventPlanner.Domain.Players;
using SwedesEventPlanner.Infrastructure.Persistence;

namespace SwedesEventPlanner.Infrastructure.Admin;

public sealed class AdminEventSetupService(EventPlannerDbContext dbContext) : IAdminEventSetupService
{
    private const string SourceSystem = EventSignupSources.GoogleForms;
    private static readonly HashSet<string> AllowedEventStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        EventStatuses.Draft,
        EventStatuses.Scheduled,
        EventStatuses.Active,
        EventStatuses.Completed,
        EventStatuses.Cancelled
    };

    private static readonly HashSet<string> AllowedRuleTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        RuleTypes.ItemCount,
        RuleTypes.PointThreshold,
        RuleTypes.ExternalCompetitionMetric,
        RuleTypes.Manual
    };

    private static readonly HashSet<string> AllowedRuleScopes = new(StringComparer.OrdinalIgnoreCase)
    {
        RuleScopes.Event,
        RuleScopes.Player,
        RuleScopes.Team
    };

    public async Task<AdminEventListResponse> ListEventsAsync(CancellationToken cancellationToken)
    {
        var events = await dbContext.Events
            .AsNoTracking()
            .OrderByDescending(eventDefinition => eventDefinition.StartsAt)
            .ThenBy(eventDefinition => eventDefinition.Name)
            .Select(eventDefinition => MapEvent(eventDefinition))
            .ToListAsync(cancellationToken);

        return new AdminEventListResponse(events);
    }

    public async Task<AdminEventSetupSummaryResponse> CreateEventAsync(
        CreateAdminEventRequest request,
        CancellationToken cancellationToken)
    {
        var slug = NormalizeSlug(request.Slug);
        if (slug.Length == 0)
        {
            throw new AdminEventSetupException("Event slug is required.");
        }

        var name = request.Name.Trim();
        if (name.Length == 0)
        {
            throw new AdminEventSetupException("Event name is required.");
        }

        if (!AllowedEventStatuses.Contains(request.Status))
        {
            throw new AdminEventSetupException("Event status is not supported.");
        }

        if (await dbContext.Events.AnyAsync(candidate => candidate.Slug == slug, cancellationToken))
        {
            throw new AdminEventSetupException("An event with this slug already exists.");
        }

        var eventDefinition = new EventDefinition
        {
            Slug = slug,
            Name = name,
            EventType = NormalizeRequired(request.EventType, EventTypes.Bingo),
            Status = request.Status.Trim().ToLowerInvariant(),
            StartsAt = request.StartsAt ?? DateTimeOffset.UtcNow,
            EndsAt = request.EndsAt,
            TimeZone = string.IsNullOrWhiteSpace(request.TimeZone)
                ? EventDefaults.TimeZone
                : request.TimeZone.Trim(),
            CreatedAt = DateTimeOffset.UtcNow,
            ConfigJson = JsonDefaults.Object
        };

        dbContext.Events.Add(eventDefinition);
        await dbContext.SaveChangesAsync(cancellationToken);

        return MapEvent(eventDefinition);
    }

    public async Task<AdminEventSetupSummaryResponse?> UpdateEventAsync(
        string eventSlug,
        UpdateAdminEventRequest request,
        CancellationToken cancellationToken)
    {
        var eventDefinition = await FindEventAsync(eventSlug, cancellationToken);
        if (eventDefinition is null)
        {
            return null;
        }

        var name = request.Name.Trim();
        if (name.Length == 0)
        {
            throw new AdminEventSetupException("Event name is required.");
        }

        eventDefinition.Name = name;
        eventDefinition.EventType = NormalizeRequired(request.EventType, EventTypes.Bingo);
        eventDefinition.StartsAt = request.StartsAt;
        eventDefinition.EndsAt = request.EndsAt;
        eventDefinition.TimeZone = string.IsNullOrWhiteSpace(request.TimeZone)
            ? EventDefaults.TimeZone
            : request.TimeZone.Trim();

        await dbContext.SaveChangesAsync(cancellationToken);
        return MapEvent(eventDefinition);
    }

    public async Task<AdminEventSetupSummaryResponse?> SetEventStatusAsync(
        string eventSlug,
        UpdateAdminEventStatusRequest request,
        CancellationToken cancellationToken)
    {
        var eventDefinition = await FindEventAsync(eventSlug, cancellationToken);
        if (eventDefinition is null)
        {
            return null;
        }

        if (!AllowedEventStatuses.Contains(request.Status))
        {
            throw new AdminEventSetupException("Event status is not supported.");
        }

        eventDefinition.Status = request.Status.Trim().ToLowerInvariant();
        await dbContext.SaveChangesAsync(cancellationToken);

        return MapEvent(eventDefinition);
    }

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

    public async Task<AdminBoardSetupResponse?> GetBoardSetupAsync(
        string eventSlug,
        CancellationToken cancellationToken)
    {
        var eventDefinition = await FindEventAsync(eventSlug, cancellationToken);
        if (eventDefinition is null)
        {
            return null;
        }

        var board = await dbContext.BingoBoards
            .AsNoTracking()
            .Where(candidate => candidate.EventId == eventDefinition.Id)
            .OrderBy(candidate => candidate.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (board is null)
        {
            return new AdminBoardSetupResponse(MapEvent(eventDefinition), null, []);
        }

        var tiles = await GetTileResponsesAsync(board.Id, cancellationToken);
        return new AdminBoardSetupResponse(MapEvent(eventDefinition), MapBoard(board), tiles);
    }

    public async Task<AdminBingoBoardResponse?> CreateBoardAsync(
        string eventSlug,
        CreateBingoBoardRequest request,
        CancellationToken cancellationToken)
    {
        var eventDefinition = await FindEventAsync(eventSlug, cancellationToken);
        if (eventDefinition is null)
        {
            return null;
        }

        var name = request.Name.Trim();
        if (name.Length == 0)
        {
            throw new AdminEventSetupException("Board name is required.");
        }

        var existing = await dbContext.BingoBoards.FirstOrDefaultAsync(
            board => board.EventId == eventDefinition.Id,
            cancellationToken);
        if (existing is not null)
        {
            existing.Name = name;
            existing.Rows = request.Rows;
            existing.Columns = request.Columns;
            await dbContext.SaveChangesAsync(cancellationToken);
            return MapBoard(existing);
        }

        var board = new BingoBoard
        {
            EventId = eventDefinition.Id,
            Name = name,
            Rows = request.Rows,
            Columns = request.Columns,
            CreatedAt = DateTimeOffset.UtcNow,
            ConfigJson = JsonDefaults.Object
        };
        dbContext.BingoBoards.Add(board);
        await dbContext.SaveChangesAsync(cancellationToken);

        return MapBoard(board);
    }

    public async Task<AdminBingoTileResponse?> CreateTileAsync(
        string eventSlug,
        long boardId,
        CreateBingoTileRequest request,
        CancellationToken cancellationToken)
    {
        var board = await FindBoardForEventAsync(eventSlug, boardId, cancellationToken);
        if (board is null)
        {
            return null;
        }

        var title = request.Title.Trim();
        if (title.Length == 0)
        {
            throw new AdminEventSetupException("Tile title is required.");
        }

        var tile = new BingoTile
        {
            BoardId = board.Id,
            Title = title,
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            PositionX = request.PositionX,
            PositionY = request.PositionY,
            SortOrder = request.SortOrder,
            ConfigJson = JsonDefaults.Object
        };
        dbContext.BingoTiles.Add(tile);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new AdminBingoTileResponse(
            tile.Id,
            tile.BoardId,
            tile.Title,
            tile.Description,
            tile.PositionX,
            tile.PositionY,
            tile.SortOrder,
            [],
            []);
    }

    public async Task<AdminBingoTileTierResponse?> CreateTileTierAsync(
        string eventSlug,
        long tileId,
        CreateBingoTileTierRequest request,
        CancellationToken cancellationToken)
    {
        var tile = await FindTileForEventAsync(eventSlug, tileId, cancellationToken);
        if (tile is null)
        {
            return null;
        }

        if (request.TierNumber <= 0)
        {
            throw new AdminEventSetupException("Tier number must be greater than zero.");
        }

        var tier = new BingoTileTier
        {
            TileId = tile.Id,
            TierNumber = request.TierNumber,
            Title = string.IsNullOrWhiteSpace(request.Title) ? null : request.Title.Trim(),
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            ScoreValue = request.ScoreValue,
            IsRequiredForBoardCompletion = request.IsRequiredForBoardCompletion,
            SortOrder = request.SortOrder,
            ConfigJson = JsonDefaults.Object
        };
        dbContext.BingoTileTiers.Add(tier);
        await dbContext.SaveChangesAsync(cancellationToken);

        return MapTier(tier);
    }

    public async Task<AdminTileRuleResponse?> CreateTileRuleAsync(
        string eventSlug,
        long tileId,
        UpsertTileRuleRequest request,
        CancellationToken cancellationToken)
    {
        var tile = await FindTileForEventAsync(eventSlug, tileId, cancellationToken);
        if (tile is null)
        {
            return null;
        }

        await ValidateRuleRequestAsync(tile.Id, request, cancellationToken);

        var rule = new TileRule
        {
            TileId = tile.Id,
            TileTierId = request.TileTierId,
            RuleType = request.RuleType.Trim().ToLowerInvariant(),
            Scope = request.Scope.Trim().ToLowerInvariant(),
            IsActive = request.IsActive,
            ConfigJson = NormalizeJsonObject(request.ConfigJson),
            CreatedAt = DateTimeOffset.UtcNow
        };
        dbContext.TileRules.Add(rule);
        await dbContext.SaveChangesAsync(cancellationToken);

        return MapRule(rule);
    }

    public async Task<AdminTileRuleResponse?> UpdateTileRuleAsync(
        string eventSlug,
        long tileId,
        long ruleId,
        UpsertTileRuleRequest request,
        CancellationToken cancellationToken)
    {
        var tile = await FindTileForEventAsync(eventSlug, tileId, cancellationToken);
        if (tile is null)
        {
            return null;
        }

        var rule = await dbContext.TileRules.FirstOrDefaultAsync(
            candidate => candidate.Id == ruleId && candidate.TileId == tile.Id,
            cancellationToken);
        if (rule is null)
        {
            return null;
        }

        await ValidateRuleRequestAsync(tile.Id, request, cancellationToken);

        rule.TileTierId = request.TileTierId;
        rule.RuleType = request.RuleType.Trim().ToLowerInvariant();
        rule.Scope = request.Scope.Trim().ToLowerInvariant();
        rule.IsActive = request.IsActive;
        rule.ConfigJson = NormalizeJsonObject(request.ConfigJson);

        await dbContext.SaveChangesAsync(cancellationToken);
        return MapRule(rule);
    }

    private async Task<EventDefinition?> FindEventAsync(string eventSlug, CancellationToken cancellationToken)
    {
        return await dbContext.Events.FirstOrDefaultAsync(
            eventDefinition => eventDefinition.Slug == eventSlug,
            cancellationToken);
    }

    private async Task<BingoBoard?> FindBoardForEventAsync(
        string eventSlug,
        long boardId,
        CancellationToken cancellationToken)
    {
        return await (
            from board in dbContext.BingoBoards
            join eventDefinition in dbContext.Events on board.EventId equals eventDefinition.Id
            where eventDefinition.Slug == eventSlug && board.Id == boardId
            select board)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private async Task<BingoTile?> FindTileForEventAsync(
        string eventSlug,
        long tileId,
        CancellationToken cancellationToken)
    {
        return await (
            from tile in dbContext.BingoTiles
            join board in dbContext.BingoBoards on tile.BoardId equals board.Id
            join eventDefinition in dbContext.Events on board.EventId equals eventDefinition.Id
            where eventDefinition.Slug == eventSlug && tile.Id == tileId
            select tile)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private async Task<List<AdminBingoTileResponse>> GetTileResponsesAsync(
        long boardId,
        CancellationToken cancellationToken)
    {
        var tiles = await dbContext.BingoTiles
            .AsNoTracking()
            .Where(tile => tile.BoardId == boardId)
            .OrderBy(tile => tile.SortOrder)
            .ThenBy(tile => tile.Id)
            .ToListAsync(cancellationToken);

        var tileIds = tiles.Select(tile => tile.Id).ToArray();
        var tiers = await dbContext.BingoTileTiers
            .AsNoTracking()
            .Where(tier => tileIds.Contains(tier.TileId))
            .OrderBy(tier => tier.SortOrder)
            .ThenBy(tier => tier.TierNumber)
            .ToListAsync(cancellationToken);
        var rules = await dbContext.TileRules
            .AsNoTracking()
            .Where(rule => tileIds.Contains(rule.TileId))
            .OrderBy(rule => rule.TileTierId)
            .ThenBy(rule => rule.Id)
            .ToListAsync(cancellationToken);

        return tiles
            .Select(tile => new AdminBingoTileResponse(
                tile.Id,
                tile.BoardId,
                tile.Title,
                tile.Description,
                tile.PositionX,
                tile.PositionY,
                tile.SortOrder,
                tiers.Where(tier => tier.TileId == tile.Id).Select(MapTier).ToList(),
                rules.Where(rule => rule.TileId == tile.Id).Select(MapRule).ToList()))
            .ToList();
    }

    private async Task ValidateRuleRequestAsync(
        long tileId,
        UpsertTileRuleRequest request,
        CancellationToken cancellationToken)
    {
        if (!AllowedRuleTypes.Contains(request.RuleType))
        {
            throw new AdminEventSetupException("Rule type is not supported.");
        }

        if (!AllowedRuleScopes.Contains(request.Scope))
        {
            throw new AdminEventSetupException("Rule scope is not supported.");
        }

        _ = NormalizeJsonObject(request.ConfigJson);

        if (request.TileTierId is null)
        {
            return;
        }

        var tierExists = await dbContext.BingoTileTiers.AnyAsync(
            tier => tier.Id == request.TileTierId && tier.TileId == tileId,
            cancellationToken);
        if (!tierExists)
        {
            throw new AdminEventSetupException("Tile tier was not found for this tile.");
        }
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

    private static AdminBingoBoardResponse MapBoard(BingoBoard board)
    {
        return new AdminBingoBoardResponse(board.Id, board.Name, board.Rows, board.Columns);
    }

    private static AdminBingoTileTierResponse MapTier(BingoTileTier tier)
    {
        return new AdminBingoTileTierResponse(
            tier.Id,
            tier.TileId,
            tier.TierNumber,
            tier.Title,
            tier.Description,
            tier.ScoreValue,
            tier.IsRequiredForBoardCompletion,
            tier.SortOrder);
    }

    private static AdminTileRuleResponse MapRule(TileRule rule)
    {
        return new AdminTileRuleResponse(
            rule.Id,
            rule.TileId,
            rule.TileTierId,
            rule.RuleType,
            rule.Scope,
            rule.IsActive,
            rule.ConfigJson);
    }

    private static string NormalizeJsonObject(string value)
    {
        try
        {
            using var document = JsonDocument.Parse(value);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                throw new AdminEventSetupException("Rule config must be a JSON object.");
            }

            return document.RootElement.GetRawText();
        }
        catch (JsonException)
        {
            throw new AdminEventSetupException("Rule config must be valid JSON.");
        }
    }

    private static string NormalizeRequired(string value, string fallback)
    {
        return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim().ToLowerInvariant();
    }

    private static string NormalizeSlug(string value)
    {
        return value.Trim().ToLowerInvariant();
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

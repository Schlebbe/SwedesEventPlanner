using System.Globalization;
using System.Text.Json;
using SwedesEventPlanner.Application.ExternalCompetitions;

namespace SwedesEventPlanner.Infrastructure.ExternalCompetitions;

public static class TempleOsrsCompetitionInfoParser
{
    public static TempleOsrsCompetitionInfo Parse(Stream jsonStream)
    {
        using var document = JsonDocument.Parse(jsonStream);
        return Parse(document.RootElement);
    }

    public static TempleOsrsCompetitionInfo Parse(string json)
    {
        using var document = JsonDocument.Parse(json);
        return Parse(document.RootElement);
    }

    private static TempleOsrsCompetitionInfo Parse(JsonElement root)
    {
        var data = root.TryGetProperty("data", out var dataElement) ? dataElement : root;
        var info = data.TryGetProperty("info", out var infoElement) ? infoElement : default;
        var metricKey = ReadString(info, "skill") ?? ReadString(info, "skill_index") ?? "overall";

        return new TempleOsrsCompetitionInfo(
            ReadString(info, "id") ?? string.Empty,
            ReadString(info, "name") ?? "TempleOSRS Competition",
            ReadBoolish(info, "team_competition") ?? false,
            metricKey,
            ReadString(info, "status"),
            ReadInt(info, "participant_count"),
            ParseParticipants(data, metricKey),
            ParseTeams(data, metricKey));
    }

    private static IReadOnlyList<TempleOsrsParticipantMetric> ParseParticipants(
        JsonElement data,
        string metricKey)
    {
        if (!data.TryGetProperty("participants", out var participants) ||
            participants.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var rows = new List<TempleOsrsParticipantMetric>();
        var rank = 1;

        foreach (var row in participants.EnumerateArray())
        {
            var runeScapeName = ReadString(row, "username") ??
                ReadString(row, "player_name") ??
                ReadString(row, "name");

            if (string.IsNullOrWhiteSpace(runeScapeName))
            {
                continue;
            }

            rows.Add(new TempleOsrsParticipantMetric(
                runeScapeName.Trim(),
                ReadString(row, "player_name_with_capitalization") ?? runeScapeName.Trim(),
                ReadLong(row, "start_xp"),
                ReadLong(row, "end_xp"),
                ReadLong(row, "gain") ?? 0L,
                ReadInt(row, "rank") ?? rank,
                ReadString(row, "team"),
                ReadString(row, "team_name"),
                ReadUnixTime(row, "last_checked_unix"),
                ReadUnixTime(row, "last_changed_unix"),
                ReadBoolish(row, "has_datapoints"),
                ReadBoolish(row, "on_hiscores")));
            rank++;
        }

        return rows;
    }

    private static IReadOnlyList<TempleOsrsTeamMetric> ParseTeams(JsonElement data, string metricKey)
    {
        if (!data.TryGetProperty("teams", out var teams) ||
            teams.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return [];
        }

        var rows = new List<TempleOsrsTeamMetric>();

        if (teams.ValueKind == JsonValueKind.Array)
        {
            var rank = 1;
            foreach (var team in teams.EnumerateArray())
            {
                rows.Add(ParseTeam(team, rank.ToString(CultureInfo.InvariantCulture), rank));
                rank++;
            }
        }
        else if (teams.ValueKind == JsonValueKind.Object)
        {
            var rank = 1;
            foreach (var property in teams.EnumerateObject())
            {
                rows.Add(ParseTeam(property.Value, property.Name, rank));
                rank++;
            }
        }

        return rows;
    }

    private static TempleOsrsTeamMetric ParseTeam(JsonElement team, string teamKey, int fallbackRank)
    {
        return new TempleOsrsTeamMetric(
            teamKey,
            ReadString(team, "name") ?? teamKey,
            ReadLong(team, "start_xp"),
            ReadLong(team, "end_xp"),
            ReadLong(team, "gain") ?? 0L,
            ReadInt(team, "rank") ?? fallbackRank,
            ReadString(team, "mvp"),
            ReadMembers(team));
    }

    private static IReadOnlyList<string> ReadMembers(JsonElement element)
    {
        if (!element.TryGetProperty("members", out var members))
        {
            return [];
        }

        if (members.ValueKind == JsonValueKind.Array)
        {
            return members
                .EnumerateArray()
                .Select(member => member.ValueKind == JsonValueKind.String ? member.GetString() : member.ToString())
                .Where(member => !string.IsNullOrWhiteSpace(member))
                .Select(member => member!.Trim())
                .ToList();
        }

        if (members.ValueKind == JsonValueKind.String)
        {
            return members.GetString()?
                .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries) ?? [];
        }

        return [];
    }

    private static string? ReadString(JsonElement element, string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Object ||
            !element.TryGetProperty(propertyName, out var value) ||
            value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return null;
        }

        return value.ValueKind == JsonValueKind.String ? value.GetString() : value.ToString();
    }

    private static int? ReadInt(JsonElement element, string propertyName)
    {
        var value = ReadLong(element, propertyName);
        return value.HasValue ? (int)value.Value : null;
    }

    private static long? ReadLong(JsonElement element, string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Object ||
            !element.TryGetProperty(propertyName, out var value))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.Number when value.TryGetInt64(out var number) => number,
            JsonValueKind.String when long.TryParse(value.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var number) => number,
            _ => null
        };
    }

    private static bool? ReadBoolish(JsonElement element, string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Object ||
            !element.TryGetProperty(propertyName, out var value))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Number when value.TryGetInt32(out var number) => number != 0,
            JsonValueKind.String when bool.TryParse(value.GetString(), out var boolean) => boolean,
            JsonValueKind.String when int.TryParse(value.GetString(), CultureInfo.InvariantCulture, out var number) => number != 0,
            _ => null
        };
    }

    private static DateTimeOffset? ReadUnixTime(JsonElement element, string propertyName)
    {
        var value = ReadLong(element, propertyName);
        return value.HasValue && value.Value > 0
            ? DateTimeOffset.FromUnixTimeSeconds(value.Value)
            : null;
    }
}

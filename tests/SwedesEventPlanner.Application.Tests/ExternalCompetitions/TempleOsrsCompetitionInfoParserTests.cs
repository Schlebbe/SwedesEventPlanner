using SwedesEventPlanner.Infrastructure.ExternalCompetitions;

namespace SwedesEventPlanner.Application.Tests.ExternalCompetitions;

public sealed class TempleOsrsCompetitionInfoParserTests
{
    [Fact]
    public void Parses_participant_rows()
    {
        var json = """
        {
          "data": {
            "info": {
              "id": "123",
              "name": "Summer XP",
              "team_competition": "0",
              "skill": "Attack",
              "participant_count": "1"
            },
            "participants": [
              {
                "username": "sebbe",
                "player_name_with_capitalization": "Sebbe",
                "gain": "1500",
                "start_xp": "100",
                "end_xp": "1600",
                "last_checked_unix": "1783000000",
                "has_datapoints": "1",
                "on_hiscores": true
              }
            ]
          }
        }
        """;

        var result = TempleOsrsCompetitionInfoParser.Parse(json);

        Assert.Equal("123", result.Id);
        Assert.Equal("Summer XP", result.Name);
        Assert.False(result.IsTeamCompetition);
        var participant = Assert.Single(result.Participants);
        Assert.Equal("sebbe", participant.RuneScapeName);
        Assert.Equal("Sebbe", participant.DisplayName);
        Assert.Equal(100, participant.StartValue);
        Assert.Equal(1600, participant.CurrentValue);
        Assert.Equal(1500, participant.GainedValue);
        Assert.True(participant.HasDatapoints);
        Assert.True(participant.OnHiscores);
    }

    [Fact]
    public void Parses_team_object_rows()
    {
        var json = """
        {
          "data": {
            "info": {
              "id": 456,
              "name": "Team KC",
              "team_competition": 1,
              "skill": "Chambers of Xeric"
            },
            "participants": [],
            "teams": {
              "1": {
                "name": "Blue",
                "gain": 77,
                "start_xp": 10,
                "end_xp": 87,
                "mvp": "Sebbe",
                "members": ["Sebbe", "Alicia"]
              }
            }
          }
        }
        """;

        var result = TempleOsrsCompetitionInfoParser.Parse(json);

        Assert.True(result.IsTeamCompetition);
        var team = Assert.Single(result.Teams);
        Assert.Equal("1", team.TempleTeamKey);
        Assert.Equal("Blue", team.TeamName);
        Assert.Equal(77, team.GainedValue);
        Assert.Equal(["Sebbe", "Alicia"], team.Members);
    }
}

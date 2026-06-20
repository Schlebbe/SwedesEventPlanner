using SwedesEventPlanner.Domain.Bingo;
using SwedesEventPlanner.Domain.Common;

namespace SwedesEventPlanner.Domain.Tests.Bingo;

public sealed class BingoDefaultsTests
{
    [Fact]
    public void Tile_tiers_default_to_required_single_point_tiers()
    {
        var tier = new BingoTileTier();

        Assert.True(tier.IsRequiredForBoardCompletion);
        Assert.Equal(1, tier.ScoreValue);
        Assert.Equal(JsonDefaults.Object, tier.ConfigJson);
    }

    [Fact]
    public void Tile_rules_default_to_active_json_config()
    {
        var rule = new TileRule
        {
            RuleType = RuleTypes.ItemCount,
            Scope = RuleScopes.Player
        };

        Assert.True(rule.IsActive);
        Assert.Equal(JsonDefaults.Object, rule.ConfigJson);
    }
}

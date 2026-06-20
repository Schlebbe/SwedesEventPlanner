using SwedesEventPlanner.Domain.Common;

namespace SwedesEventPlanner.Domain.Tests.Common;

public sealed class JsonDefaultsTests
{
    [Fact]
    public void Json_defaults_are_empty_json_literals()
    {
        Assert.Equal("{}", JsonDefaults.Object);
        Assert.Equal("[]", JsonDefaults.Array);
    }
}

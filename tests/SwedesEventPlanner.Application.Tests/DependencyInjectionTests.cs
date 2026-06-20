using Microsoft.Extensions.DependencyInjection;
using SwedesEventPlanner.Application.Clock;

namespace SwedesEventPlanner.Application.Tests;

public sealed class DependencyInjectionTests
{
    [Fact]
    public void AddApplication_registers_clock()
    {
        var services = new ServiceCollection();

        services.AddApplication();

        using var provider = services.BuildServiceProvider();
        var clock = provider.GetRequiredService<IClock>();

        Assert.True(clock.UtcNow.Offset == TimeSpan.Zero);
    }
}

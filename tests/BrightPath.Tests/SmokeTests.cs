using BrightPath.Api;
using Xunit;

namespace BrightPath.Tests;

public class SmokeTests
{
    [Fact]
    public void PinnedClockReturnsConfiguredTime()
    {
        var now = new DateTime(2026, 3, 5, 10, 0, 0);
        Assert.Equal(now, new PinnedClock(now).Now);
    }

    [Fact]
    public void AppsettingsPinsNow()
    {
        var json = File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "appsettings.json"));
        Assert.Contains("2026-03-05T10:00:00", json);
    }
}

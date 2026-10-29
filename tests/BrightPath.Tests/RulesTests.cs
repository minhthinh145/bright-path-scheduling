using BrightPath.Api;
using Xunit;

namespace BrightPath.Tests;

public class RulesTests
{
    private static readonly DateTime T = new(2026, 3, 6, 10, 0, 0); // Friday

    [Fact]
    public void StartAt0900IsAllowed() =>
        Assert.False(Rules.OutsideHours(new DateTime(2026, 3, 6, 9, 0, 0), 60));

    [Fact]
    public void Before0900IsOutside() =>
        Assert.True(Rules.OutsideHours(new DateTime(2026, 3, 6, 8, 59, 0), 60));

    [Fact]
    public void EndExactly2130IsAllowed() =>
        Assert.False(Rules.OutsideHours(new DateTime(2026, 3, 6, 20, 0, 0), 90));

    [Fact]
    public void Start2100For60IsOutside() =>
        Assert.True(Rules.OutsideHours(new DateTime(2026, 3, 6, 21, 0, 0), 60));

    [Fact]
    public void Start2100For90IsOutside() =>
        Assert.True(Rules.OutsideHours(new DateTime(2026, 3, 6, 21, 0, 0), 90));

    [Fact]
    public void MondayIsClosed() =>
        Assert.True(Rules.ClosedDay(new DateTime(2026, 3, 9, 9, 0, 0)));

    [Fact]
    public void CutoffExactly1600IsNotAfter() =>
        Assert.False(Rules.AfterCutoff(T, T.Date.AddDays(-1).AddHours(16)));

    [Fact]
    public void FourHoursExactlyIsNotLate() =>
        Assert.False(Rules.Late(T, T.AddHours(-4), "family"));

    [Fact]
    public void ThreeHoursIsLate() =>
        Assert.True(Rules.Late(T, T.AddHours(-3), "family"));

    [Fact]
    public void CentreCancelIsNeverLate() =>
        Assert.False(Rules.Late(T, T.AddHours(-1), "centre"));
}

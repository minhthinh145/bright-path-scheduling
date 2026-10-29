using BrightPath.Api;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BrightPath.Tests;

// Pinned "now" for the export week: Thursday 2026-03-05 10:00.
[Collection("seed")]
public class SeedTests : IAsyncLifetime
{
    private static readonly DateTime PinnedNow = new(2026, 3, 5, 10, 0, 0);
    private readonly SeedDb _fx;
    private string _report = "";

    public SeedTests(SeedDb fx) => _fx = fx;

    public async Task InitializeAsync()
    {
        await using var db = _fx.CreateDb();
        await db.Database.ExecuteSqlRawAsync(
            "TRUNCATE booking_changes, bookings, tutors");
        _report = await Seed.Import(db, PostgresFixture.SeedDir, PinnedNow);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task LoadsAll34Rows()
    {
        await using var db = _fx.CreateDb();
        Assert.Equal(34, await db.Bookings.CountAsync());
        Assert.Equal(3, await db.Tutors.CountAsync());
    }

    [Fact]
    public async Task ExamPairSharesLessonId()
    {
        await using var db = _fx.CreateDb();
        var l9 = await db.Bookings.FindAsync("L009");
        var l10 = await db.Bookings.FindAsync("L010");
        Assert.NotNull(l9);
        Assert.Equal(l9!.LessonId, l10!.LessonId);
    }

    [Fact]
    public async Task ClashRowsAreFlaggedPerRule()
    {
        await using var db = _fx.CreateDb();
        Assert.Equal("student_clash", (await db.Bookings.FindAsync("L008"))!.ImportedClash);
        Assert.Equal("tutor_clash", (await db.Bookings.FindAsync("L034"))!.ImportedClash);
        Assert.Equal(2, await db.Bookings.CountAsync(b => b.ImportedClash != null));
    }

    [Fact]
    public async Task CancelledRowsGetChangeRows()
    {
        await using var db = _fx.CreateDb();
        var changes = await db.BookingChanges.Where(c => c.Kind == "cancelled").ToListAsync();
        Assert.Equal(2, changes.Count);
        Assert.All(changes, c => Assert.True(c.AfterCutoff));
        // L005 family cancelled ~5.75h before -> not late; L017 centre -> never late.
        Assert.All(changes, c => Assert.False(c.Late));
    }

    [Fact]
    public async Task CancelledRowsConvertOffsetToNaiveLocal()
    {
        await using var db = _fx.CreateDb();
        var l5 = await db.Bookings.FindAsync("L005");
        Assert.Equal(new DateTime(2026, 3, 3, 8, 15, 0), l5!.CancelledAt);
        Assert.Equal("family", l5.CancelledBy);
        Assert.Equal("centre", (await db.Bookings.FindAsync("L017"))!.CancelledBy);
    }

    [Fact]
    public void ReportListsTheFindings()
    {
        Assert.Contains("L008", _report);
        Assert.Contains("L034", _report);
        Assert.Contains("FUTURE", _report);
        Assert.Contains("L032", _report);
        Assert.Contains("T1", _report);
        Assert.Contains("L009", _report);
        Assert.Contains("L010", _report);
    }
}

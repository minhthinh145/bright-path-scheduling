using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using BrightPath.Api;
using Xunit;

namespace BrightPath.Tests;

[Collection("api")]
public class ChangeTests
{
    private static readonly DateTime Pinned = new(2026, 3, 5, 10, 0, 0);
    private readonly ApiFixture _fx;
    private HttpClient Http => _fx.Client;

    public ChangeTests(ApiFixture fx)
    {
        _fx = fx;
        _fx.Clock.Now = Pinned;
    }

    private static JsonElement Prop(JsonElement e, string name) => e.GetProperty(name);

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private static async Task<(Booking booking, BookingChange change)> Ok(HttpResponseMessage r)
    {
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        var doc = await r.Content.ReadFromJsonAsync<JsonElement>();
        var booking = doc.GetProperty("booking").Deserialize<Booking>(Json)!;
        var change = doc.GetProperty("change").Deserialize<BookingChange>(Json)!;
        return (booking, change);
    }

    private static async Task<string> Code(HttpResponseMessage r)
    {
        Assert.Equal(HttpStatusCode.Conflict, r.StatusCode);
        var body = await r.Content.ReadFromJsonAsync<Dictionary<string, string>>();
        return body!["code"];
    }

    [Fact]
    public async Task CancelBeforeCutoffIsNotAfterCutoff()
    {
        // L018 is 03-06; cutoff is 03-05 16:00; now is 03-05 10:00.
        var (_, change) = await Ok(await Http.PostAsJsonAsync(
            "/bookings/L018/cancel", new { cancelledBy = "centre" }));
        Assert.Equal("cancelled", change.Kind);
        Assert.False(change.AfterCutoff);
    }

    [Fact]
    public async Task CancelAfterCutoffIsMarked()
    {
        _fx.Clock.Now = new DateTime(2026, 3, 5, 17, 0, 0);
        var (_, change) = await Ok(await Http.PostAsJsonAsync(
            "/bookings/L019/cancel", new { cancelledBy = "centre" }));
        Assert.True(change.AfterCutoff);
    }

    [Fact]
    public async Task FamilyCancelInside4hIsLate()
    {
        // L021 is 03-06 11:30; cancel at 08:30 = 3h before.
        _fx.Clock.Now = new DateTime(2026, 3, 6, 8, 30, 0);
        var (_, change) = await Ok(await Http.PostAsJsonAsync(
            "/bookings/L021/cancel", new { cancelledBy = "family" }));
        Assert.True(change.Late);
    }

    [Fact]
    public async Task FamilyCancelExactly4hIsNotLate()
    {
        // L022 is 03-06 13:00; cancel at 09:00 = exactly 4h before.
        _fx.Clock.Now = new DateTime(2026, 3, 6, 9, 0, 0);
        var (_, change) = await Ok(await Http.PostAsJsonAsync(
            "/bookings/L022/cancel", new { cancelledBy = "family" }));
        Assert.False(change.Late);
    }

    [Fact]
    public async Task CentreCancelInside4hIsNotLate()
    {
        // L024 is 03-06 16:00; centre cancels at 15:00.
        _fx.Clock.Now = new DateTime(2026, 3, 6, 15, 0, 0);
        var (_, change) = await Ok(await Http.PostAsJsonAsync(
            "/bookings/L024/cancel", new { cancelledBy = "centre" }));
        Assert.False(change.Late);
    }

    [Fact]
    public async Task CancelFreesTheSlotForRebooking()
    {
        // L030 is 03-07 15:00 T1 R1.
        await Ok(await Http.PostAsJsonAsync("/bookings/L030/cancel", new { cancelledBy = "family" }));
        var r = await Http.PostAsJsonAsync("/bookings",
            new { studentName = "QA Rebook", tutorId = "T3", roomId = "R1",
                  startsAt = "2026-03-07T15:00:00", durationMin = 60 });
        Assert.Equal(HttpStatusCode.Created, r.StatusCode);
    }

    [Fact]
    public async Task CancellingACancelledBookingIsNotBooked()
    {
        // L005 is already cancelled in the export.
        var r = await Http.PostAsJsonAsync("/bookings/L005/cancel", new { cancelledBy = "family" });
        Assert.Equal("NOT_BOOKED", await Code(r));
    }

    [Fact]
    public async Task CancellingAPastLessonIsInPast()
    {
        // L007 ran on 03-04, before the pinned now.
        var r = await Http.PostAsJsonAsync("/bookings/L007/cancel", new { cancelledBy = "family" });
        Assert.Equal("IN_PAST", await Code(r));
    }

    [Fact]
    public async Task MovingAPastLessonIsInPast()
    {
        var r = await Http.PostAsJsonAsync("/bookings/L008/move",
            new { startsAt = "2026-03-10T11:00:00" });
        Assert.Equal("IN_PAST", await Code(r));
    }

    [Fact]
    public async Task MoveAfterOldDaysCutoffIsMarked()
    {
        // L025 is 03-06; its cutoff (03-05 16:00) has passed at now 03-05 17:00.
        _fx.Clock.Now = new DateTime(2026, 3, 5, 17, 0, 0);
        var (_, change) = await Ok(await Http.PostAsJsonAsync("/bookings/L025/move",
            new { startsAt = "2026-03-10T11:00:00", roomId = "R3" }));
        Assert.Equal("moved", change.Kind);
        Assert.True(change.AfterCutoff);
    }

    [Fact]
    public async Task MovingACancelledBookingIsNotBooked()
    {
        var r = await Http.PostAsJsonAsync("/bookings/L005/move",
            new { startsAt = "2026-03-07T10:00:00" });
        Assert.Equal("NOT_BOOKED", await Code(r));
    }

    [Fact]
    public async Task MoveWritesOldAndNewAndLeavesPair()
    {
        // L031 is 03-08 10:00 T1 R1.
        var (booking, change) = await Ok(await Http.PostAsJsonAsync("/bookings/L031/move",
            new { startsAt = "2026-03-08T11:00:00", roomId = "R2" }));
        Assert.Equal("moved", change.Kind);
        Assert.Equal(new DateTime(2026, 3, 8, 10, 0, 0), change.OldStartsAt);
        Assert.Equal("R1", change.OldRoomId);
        Assert.Equal(new DateTime(2026, 3, 8, 11, 0, 0), change.NewStartsAt);
        Assert.Equal("R2", change.NewRoomId);
        Assert.Equal("R2", booking.RoomId);
    }

    [Fact]
    public async Task MoverLeavesThePair()
    {
        await using var db = _fx.CreateDb();
        var lesson = Guid.NewGuid();
        var t = new DateTime(2026, 3, 13, 10, 0, 0);
        db.Bookings.AddRange(
            new Booking { Id = "LP1", LessonId = lesson, StudentName = "Pair One",
                TutorId = "T2", RoomId = "R6", StartsAt = t, DurationMin = 60 },
            new Booking { Id = "LP2", LessonId = lesson, StudentName = "Pair Two",
                TutorId = "T2", RoomId = "R6", StartsAt = t, DurationMin = 60 });
        await db.SaveChangesAsync();

        var (moved, _) = await Ok(await Http.PostAsJsonAsync("/bookings/LP2/move",
            new { startsAt = "2026-03-13T11:00:00" }));
        Assert.NotEqual(lesson, moved.LessonId);
    }

    [Fact]
    public async Task MoveIntoAClashIsRejectedAndChangesNothing()
    {
        // L033 is 03-10 09:00 T1 R1; R2 holds L034 at that time.
        var r = await Http.PostAsJsonAsync("/bookings/L033/move",
            new { startsAt = "2026-03-10T09:00:00", roomId = "R2" });
        Assert.Equal("ROOM_CLASH", await Code(r));
        await using var db = _fx.CreateDb();
        var unchanged = await db.Bookings.FindAsync("L033");
        Assert.Equal(new DateTime(2026, 3, 10, 9, 0, 0), unchanged!.StartsAt);
        Assert.Equal("R1", unchanged.RoomId);
        Assert.Empty(db.BookingChanges.Where(c => c.BookingId == "L033" && c.Kind == "moved"));
    }

    [Fact]
    public async Task ChangesEndpointReturnsHistory()
    {
        // L017 arrives cancelled in the export, so its history is already there.
        var changes = await Http.GetFromJsonAsync<List<BookingChange>>("/bookings/L017/changes");
        Assert.Contains(changes!, c => c.Kind == "cancelled" && c.AfterCutoff);
        var missing = await Http.GetAsync("/bookings/L999/changes");
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }

    [Fact]
    public async Task UnknownBookingIs404()
    {
        Assert.Equal(HttpStatusCode.NotFound,
            (await Http.PostAsJsonAsync("/bookings/L999/move", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await Http.PostAsJsonAsync("/bookings/L999/cancel", new { cancelledBy = "family" })).StatusCode);
    }
}

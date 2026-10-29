using System.Net;
using System.Net.Http.Json;
using BrightPath.Api;
using Xunit;

namespace BrightPath.Tests;

[Collection("api")]
public class BookingTests
{
    private readonly ApiFixture _fx;
    private HttpClient Http => _fx.Client;
    public BookingTests(ApiFixture fx)
    {
        _fx = fx;
        _fx.Clock.Now = new DateTime(2026, 3, 5, 10, 0, 0);
    }

    private static Dictionary<string, object?> Req(string student, string tutor, string room,
        string startsAt, int dur) => new()
    {
        ["studentName"] = student, ["tutorId"] = tutor, ["roomId"] = room,
        ["startsAt"] = startsAt, ["durationMin"] = dur,
    };

    private static async Task<string> Code(HttpResponseMessage r)
    {
        Assert.Equal(HttpStatusCode.Conflict, r.StatusCode);
        var body = await r.Content.ReadFromJsonAsync<Dictionary<string, string>>();
        Assert.True(body!.ContainsKey("code") && body.ContainsKey("rule") && body.ContainsKey("message"));
        return body["code"];
    }

    [Fact]
    public async Task ValidBookingReturns201()
    {
        var r = await Http.PostAsJsonAsync("/bookings",
            Req("QA New Student", "T3", "R5", "2026-03-10T11:00:00", 60));
        Assert.Equal(HttpStatusCode.Created, r.StatusCode);
        var b = await r.Content.ReadFromJsonAsync<Booking>();
        Assert.True(int.Parse(b!.Id[1..]) > 34); // ids continue after the import's max L034
    }

    [Fact]
    public async Task StudentClashPartialOverlapIsRejected()
    {
        // Nguyen Thi Ha has L016 on 03-05 14:30-16:00 (T1, R1).
        var r = await Http.PostAsJsonAsync("/bookings",
            Req("Nguyen Thi Ha", "T2", "R5", "2026-03-05T15:00:00", 60));
        var body = await r.Content.ReadFromJsonAsync<Dictionary<string, string>>();
        Assert.Equal(HttpStatusCode.Conflict, r.StatusCode);
        Assert.Equal("STUDENT_CLASH", body!["code"]);
        Assert.Equal("a student cannot be in two lessons at once", body["rule"]);
        Assert.Equal("Nguyen Thi Ha already has a lesson at that time", body["message"]);
    }

    [Fact]
    public async Task TutorClashIsRejected()
    {
        // T1 is busy 14:30-16:00 on 03-05 in R1.
        var r = await Http.PostAsJsonAsync("/bookings",
            Req("QA Tutor Clash", "T1", "R5", "2026-03-05T15:00:00", 60));
        Assert.Equal("TUTOR_CLASH", await Code(r));
    }

    [Fact]
    public async Task RoomClashIsRejected()
    {
        // L034 sits in R2 on 03-10 09:00 with tutor_clash; its flag only lifts the tutor rule.
        var r = await Http.PostAsJsonAsync("/bookings",
            Req("QA Room Clash", "T2", "R2", "2026-03-10T09:00:00", 60));
        Assert.Equal("ROOM_CLASH", await Code(r));
    }

    [Fact]
    public async Task MondayIsClosed()
    {
        var r = await Http.PostAsJsonAsync("/bookings",
            Req("QA Monday", "T3", "R5", "2026-03-09T09:00:00", 60));
        Assert.Equal("CLOSED_DAY", await Code(r));
    }

    [Fact]
    public async Task LessonPast2130IsOutsideHours()
    {
        var r = await Http.PostAsJsonAsync("/bookings",
            Req("QA Hours", "T3", "R5", "2026-03-10T21:00:00", 90));
        Assert.Equal("OUTSIDE_HOURS", await Code(r));
    }

    [Fact]
    public async Task PastBookingIsRejected()
    {
        var r = await Http.PostAsJsonAsync("/bookings",
            Req("QA Past", "T3", "R5", "2026-03-01T09:00:00", 60));
        Assert.Equal("IN_PAST", await Code(r));
    }

    [Fact]
    public async Task TutorWithSevenLessonsIsFull()
    {
        // T1 already has 7 lessons on 2026-03-06 from the export.
        var r = await Http.PostAsJsonAsync("/bookings",
            Req("QA Full", "T1", "R5", "2026-03-06T20:00:00", 60));
        Assert.Equal("TUTOR_DAY_FULL", await Code(r));
    }

    [Fact]
    public async Task SixthLessonIsAllowedSeventhRefused()
    {
        await using var db = _fx.CreateDb();
        for (var i = 0; i < 5; i++)
            db.Bookings.Add(new Booking
            {
                Id = $"LT{i}", LessonId = Guid.NewGuid(), StudentName = $"Pad {i}",
                TutorId = "T3", RoomId = $"R{i + 1}", DurationMin = 60,
                StartsAt = new DateTime(2026, 3, 12, 9 + i, 0, 0),
            });
        await db.SaveChangesAsync();

        var ok = await Http.PostAsJsonAsync("/bookings",
            Req("QA Sixth", "T3", "R6", "2026-03-12T14:00:00", 60));
        Assert.Equal(HttpStatusCode.Created, ok.StatusCode);

        var r = await Http.PostAsJsonAsync("/bookings",
            Req("QA Seventh", "T3", "R6", "2026-03-12T15:00:00", 60));
        Assert.Equal("TUTOR_DAY_FULL", await Code(r));
    }

    [Fact]
    public async Task JoiningASingleStudentLessonMakesAPair()
    {
        await using var db = _fx.CreateDb();
        var l28 = await db.Bookings.FindAsync("L028");
        var r = await Http.PostAsJsonAsync("/bookings",
            new { studentName = "QA Pair Mate", joinLessonId = l28!.LessonId });
        Assert.Equal(HttpStatusCode.Created, r.StatusCode);
        var b = await r.Content.ReadFromJsonAsync<Booking>();
        Assert.Equal(l28.LessonId, b!.LessonId);
    }

    [Fact]
    public async Task ThirdStudentOnAPairIsPairFull()
    {
        await using var db = _fx.CreateDb();
        var l9 = await db.Bookings.FindAsync("L009");
        var r = await Http.PostAsJsonAsync("/bookings",
            new { studentName = "QA Third", joinLessonId = l9!.LessonId });
        Assert.Equal("PAIR_FULL", await Code(r));
    }

    [Fact]
    public async Task MalformedInputIs422()
    {
        var blank = await Http.PostAsJsonAsync("/bookings",
            Req("  ", "T3", "R5", "2026-03-10T11:00:00", 60));
        Assert.Equal((HttpStatusCode)422, blank.StatusCode);

        var dur = await Http.PostAsJsonAsync("/bookings",
            Req("QA Dur", "T3", "R5", "2026-03-10T11:00:00", 45));
        Assert.Equal((HttpStatusCode)422, dur.StatusCode);

        var tutor = await Http.PostAsJsonAsync("/bookings",
            Req("QA Tutor", "T9", "R5", "2026-03-10T11:00:00", 60));
        Assert.Equal((HttpStatusCode)422, tutor.StatusCode);

        var room = await Http.PostAsJsonAsync("/bookings",
            Req("QA Room", "T3", "R9", "2026-03-10T11:00:00", 60));
        Assert.Equal((HttpStatusCode)422, room.StatusCode);
    }

    [Fact]
    public async Task ListByDateAndTutor()
    {
        var r = await Http.GetFromJsonAsync<List<Booking>>("/bookings?date=2026-03-03");
        Assert.Equal(6, r!.Count);
        var t1 = await Http.GetFromJsonAsync<List<Booking>>("/bookings?date=2026-03-06&tutorId=T1");
        Assert.Equal(7, t1!.Count);
        var none = await Http.GetFromJsonAsync<List<Booking>>("/bookings?date=2026-03-06&tutorId=T3");
        Assert.Single(none!);
    }
}

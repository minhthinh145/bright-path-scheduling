using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace BrightPath.Api;

public record CreateBookingRequest(string? StudentName, string? TutorId, string? RoomId,
    DateTime? StartsAt, int? DurationMin, Guid? JoinLessonId);

public class RuleException : Exception
{
    public string Code { get; }
    public string Rule { get; }
    public RuleException(string code, string rule, string message) : base(message)
    { Code = code; Rule = rule; }
}

public static class Endpoints
{
    public static void Map(WebApplication app)
    {
        app.MapPost("/bookings", Create);
        app.MapGet("/bookings", List);
    }

    internal static IResult Conflict(string code, string rule, string message) =>
        Results.Json(new { code, rule, message }, statusCode: 409);

    internal static IResult Bad(string message) =>
        Results.Json(new { message }, statusCode: 422);

    // Exclusion-constraint hit: map constraint name to the business code.
    internal static IResult Wrap(DbUpdateException ex, string student, string tutorId,
        string roomId, DateTime start)
    {
        for (Exception? e = ex; e != null; e = e.InnerException)
            if (e is PostgresException pg && pg.SqlState == "23P01"
                && Rules.ConstraintInfo(pg.ConstraintName ?? "") is { } info)
                return Conflict(info.Code, info.Rule, info.Code switch
                {
                    "STUDENT_CLASH" => $"{student} already has a lesson at that time",
                    "TUTOR_CLASH" => $"tutor {tutorId} is busy at that time",
                    _ => $"room {roomId} is taken at that time",
                });
        throw ex;
    }

    // Shared checks for a new position (create and move). Throws RuleException.
    internal static void CheckSlot(DateTime start, int durationMin, DateTime now)
    {
        if (Rules.ClosedDay(start))
            throw new RuleException("CLOSED_DAY", "the centre is closed on Mondays",
                $"{start:yyyy-MM-dd} is a Monday");
        if (Rules.OutsideHours(start, durationMin))
            throw new RuleException("OUTSIDE_HOURS", "lessons run 09:00-21:30",
                $"a lesson {start:HH:mm}-{start.AddMinutes(durationMin):HH:mm} is outside 09:00-21:30");
        if (Rules.InPast(start, now))
            throw new RuleException("IN_PAST", "cannot book in the past",
                $"{start:yyyy-MM-dd HH:mm} is before now ({now:yyyy-MM-dd HH:mm})");
    }

    // The tutor's distinct lessons on that date (a pair counts once),
    // excluding cancelled bookings and, for a move, the moving row itself.
    internal static async Task<int> TutorLessonCount(BrightPathDb db, string tutorId,
        DateTime date, string? excludeBookingId)
    {
        var q = db.Bookings.Where(b => b.TutorId == tutorId
            && b.Status != "cancelled"
            && b.StartsAt.Date == date.Date
            && b.Id != excludeBookingId);
        return (await q.Select(b => b.LessonId).Distinct().ToListAsync()).Count;
    }

    // Lock the tutor row so two concurrent bookings cannot both pass 6-a-day.
    internal static async Task LockTutor(BrightPathDb db, string tutorId) =>
        await db.Database.ExecuteSqlAsync($"SELECT id FROM tutors WHERE id = {tutorId} FOR UPDATE");

    internal static async Task<string> NextId(BrightPathDb db) =>
        $"L{await db.Database.SqlQuery<long>($"SELECT nextval('booking_id_seq') AS \"Value\"").SingleAsync():D3}";

    private static async Task<IResult> Create(CreateBookingRequest req, BrightPathDb db, IClock clock)
    {
        var now = clock.Now;
        if (string.IsNullOrWhiteSpace(req.StudentName)) return Bad("studentName is required");

        string tutorId, roomId; DateTime start; int durationMin; Guid lessonId;
        if (req.JoinLessonId is Guid joinId)
        {
            var sib = await db.Bookings.FirstOrDefaultAsync(b => b.LessonId == joinId && b.Status != "cancelled");
            if (sib is null) return Bad("joinLessonId does not match a lesson");
            var members = await db.Bookings.CountAsync(b => b.LessonId == joinId && b.Status != "cancelled");
            if (members >= 2)
                return Conflict("PAIR_FULL", "an exam pair has at most 2 students",
                    "that lesson already has 2 students");
            (tutorId, roomId, start, durationMin, lessonId) =
                (sib.TutorId, sib.RoomId, sib.StartsAt, sib.DurationMin, joinId);
        }
        else
        {
            if (req.StartsAt is null) return Bad("startsAt is required");
            if (req.DurationMin is not (60 or 90)) return Bad("durationMin must be 60 or 90");
            if (string.IsNullOrWhiteSpace(req.TutorId) || !await db.Tutors.AnyAsync(t => t.Id == req.TutorId))
                return Bad("unknown tutorId");
            if (string.IsNullOrWhiteSpace(req.RoomId) || !await db.Rooms.AnyAsync(r => r.Id == req.RoomId))
                return Bad("unknown roomId");
            (tutorId, roomId, start, durationMin, lessonId) =
                (req.TutorId, req.RoomId, req.StartsAt.Value, req.DurationMin.Value, Guid.NewGuid());
        }

        try { CheckSlot(start, durationMin, now); }
        catch (RuleException re) { return Conflict(re.Code, re.Rule, re.Message); }

        await using var tx = await db.Database.BeginTransactionAsync();
        try
        {
            await LockTutor(db, tutorId);
            var lessonIds = await db.Bookings
                .Where(b => b.TutorId == tutorId && b.Status != "cancelled" && b.StartsAt.Date == start.Date)
                .Select(b => b.LessonId).Distinct().ToListAsync();
            if (!lessonIds.Contains(lessonId) && lessonIds.Count >= 6)
                return Conflict("TUTOR_DAY_FULL", "at most 6 lessons per tutor per day",
                    $"tutor already has {lessonIds.Count} lessons on {start:yyyy-MM-dd}");

            var booking = new Booking
            {
                Id = await NextId(db), LessonId = lessonId, StudentName = req.StudentName.Trim(),
                TutorId = tutorId, RoomId = roomId, StartsAt = start, DurationMin = durationMin,
            };
            db.Bookings.Add(booking);
            db.BookingChanges.Add(new BookingChange
            {
                BookingId = booking.Id, Kind = "added", NewStartsAt = start, NewRoomId = roomId,
                ChangedAt = now, AfterCutoff = Rules.AfterCutoff(start, now),
            });
            await db.SaveChangesAsync();
            await tx.CommitAsync();
            return Results.Created($"/bookings/{booking.Id}", booking);
        }
        catch (DbUpdateException ex)
        {
            await tx.RollbackAsync();
            return Wrap(ex, req.StudentName.Trim(), tutorId, roomId, start);
        }
    }

    private static async Task<IResult> List(string? date, string? tutorId, BrightPathDb db)
    {
        if (!DateOnly.TryParse(date, out var d)) return Bad("date must be YYYY-MM-DD");
        var day = d.ToDateTime(TimeOnly.MinValue);
        var q = db.Bookings.Where(b => b.StartsAt >= day && b.StartsAt < day.AddDays(1));
        if (!string.IsNullOrWhiteSpace(tutorId)) q = q.Where(b => b.TutorId == tutorId);
        return Results.Ok(await q.OrderBy(b => b.StartsAt).ToListAsync());
    }
}

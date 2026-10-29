using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace BrightPath.Api;

public class BookingService(BrightPathDb db, IClock clock)
{
    private record Slot(string TutorId, string RoomId, DateTime Start, int DurationMin, Guid LessonId);

    private IQueryable<Booking> Active => db.Bookings.Where(b => b.Status != BookingStatus.Cancelled);

    public async Task<BookingResponse> Book(CreateBookingRequest req)
    {
        var now = clock.Now;
        if (string.IsNullOrWhiteSpace(req.StudentName))
            throw new BadInputException("studentName is required");
        var student = req.StudentName.Trim();

        var slot = req.JoinLessonId is Guid pairId ? await PairSlot(pairId) : await NewSlot(req);
        CheckSlot(slot.Start, slot.DurationMin, now);

        await using var tx = await db.Database.BeginTransactionAsync();
        await CheckTutorDay(slot.TutorId, slot.Start, slot.LessonId, null);
        var booking = new Booking
        {
            Id = await NextId(), LessonId = slot.LessonId, StudentName = student,
            TutorId = slot.TutorId, RoomId = slot.RoomId, StartsAt = slot.Start,
            DurationMin = slot.DurationMin,
        };
        db.Bookings.Add(booking);
        db.BookingChanges.Add(new BookingChange
        {
            BookingId = booking.Id, Kind = "added", NewStartsAt = slot.Start, NewRoomId = slot.RoomId,
            ChangedAt = now, AfterCutoff = Rules.AfterCutoff(slot.Start, now),
        });
        await SaveOrTranslate(student, slot.TutorId, slot.RoomId);
        await tx.CommitAsync();
        return booking.ToResponse();
    }

    public async Task<ChangeResult> Move(string id, MoveRequest req)
    {
        var now = clock.Now;
        var booking = await FindChangeable(id, "move");

        var newStart = req.StartsAt ?? booking.StartsAt;
        var newRoom = string.IsNullOrWhiteSpace(req.RoomId) ? booking.RoomId : req.RoomId;
        if (!await db.Rooms.AnyAsync(r => r.Id == newRoom)) throw new BadInputException("unknown roomId");

        CheckSlot(newStart, booking.DurationMin, now);

        // The mover leaves its lesson/pair, so count the new position as a new lesson.
        var newLessonId = Guid.NewGuid();
        await using var tx = await db.Database.BeginTransactionAsync();
        await CheckTutorDay(booking.TutorId, newStart, newLessonId, booking.Id);
        var change = new BookingChange
        {
            BookingId = booking.Id, Kind = "moved",
            OldStartsAt = booking.StartsAt, OldRoomId = booking.RoomId,
            NewStartsAt = newStart, NewRoomId = newRoom,
            ChangedAt = now,
            // After cut-off on either day counts: the old day's tutor was already told.
            AfterCutoff = Rules.AfterCutoff(booking.StartsAt, now) || Rules.AfterCutoff(newStart, now),
        };
        booking.StartsAt = newStart;
        booking.RoomId = newRoom;
        booking.LessonId = newLessonId;
        db.BookingChanges.Add(change);
        await SaveOrTranslate(booking.StudentName, booking.TutorId, newRoom);
        await tx.CommitAsync();
        return new ChangeResult(booking.ToResponse(), change.ToResponse());
    }

    public async Task<ChangeResult> Cancel(string id, CancelRequest req)
    {
        var now = clock.Now;
        if (req.CancelledBy is not ("family" or "centre"))
            throw new BadInputException("cancelledBy must be family or centre");
        var booking = await FindChangeable(id, "be cancelled");

        var change = new BookingChange
        {
            BookingId = booking.Id, Kind = "cancelled",
            OldStartsAt = booking.StartsAt, OldRoomId = booking.RoomId,
            ChangedAt = now,
            AfterCutoff = Rules.AfterCutoff(booking.StartsAt, now),
            Late = Rules.Late(booking.StartsAt, now, req.CancelledBy),
        };
        booking.Status = BookingStatus.Cancelled;
        booking.CancelledAt = now;
        booking.CancelledBy = req.CancelledBy;
        db.BookingChanges.Add(change);
        await db.SaveChangesAsync();
        return new ChangeResult(booking.ToResponse(), change.ToResponse());
    }

    public async Task<List<BookingResponse>> List(string? date, string? tutorId)
    {
        if (!DateOnly.TryParse(date, out var d)) throw new BadInputException("date must be YYYY-MM-DD");
        var day = d.ToDateTime(TimeOnly.MinValue);
        var q = db.Bookings.Where(b => b.StartsAt >= day && b.StartsAt < day.AddDays(1));
        if (!string.IsNullOrWhiteSpace(tutorId)) q = q.Where(b => b.TutorId == tutorId);
        return (await q.OrderBy(b => b.StartsAt).ToListAsync()).Select(b => b.ToResponse()).ToList();
    }

    public async Task<List<ChangeResponse>> Changes(string id)
    {
        if (!await db.Bookings.AnyAsync(b => b.Id == id)) throw new NotFoundException();
        var rows = await db.BookingChanges.Where(c => c.BookingId == id).OrderBy(c => c.Id).ToListAsync();
        return rows.Select(c => c.ToResponse()).ToList();
    }

    private async Task<Slot> PairSlot(Guid lessonId)
    {
        var members = await Active.Where(b => b.LessonId == lessonId).ToListAsync();
        if (members.Count == 0) throw new BadInputException("joinLessonId does not match a lesson");
        if (members.Count >= 2)
            throw new RuleException("PAIR_FULL", "an exam pair has at most 2 students",
                "that lesson already has 2 students");
        var s = members[0];
        return new Slot(s.TutorId, s.RoomId, s.StartsAt, s.DurationMin, lessonId);
    }

    private async Task<Slot> NewSlot(CreateBookingRequest req)
    {
        if (req.StartsAt is null) throw new BadInputException("startsAt is required");
        if (req.DurationMin is not (60 or 90)) throw new BadInputException("durationMin must be 60 or 90");
        if (string.IsNullOrWhiteSpace(req.TutorId) || !await db.Tutors.AnyAsync(t => t.Id == req.TutorId))
            throw new BadInputException("unknown tutorId");
        if (string.IsNullOrWhiteSpace(req.RoomId) || !await db.Rooms.AnyAsync(r => r.Id == req.RoomId))
            throw new BadInputException("unknown roomId");
        return new Slot(req.TutorId, req.RoomId, req.StartsAt.Value, req.DurationMin.Value, Guid.NewGuid());
    }

    private async Task<Booking> FindChangeable(string id, string action)
    {
        var now = clock.Now;
        var booking = await db.Bookings.FindAsync(id) ?? throw new NotFoundException();
        if (booking.Status != BookingStatus.Booked)
            throw new RuleException("NOT_BOOKED", $"only a booked lesson can {action}",
                $"booking is {booking.Status}");
        if (booking.StartsAt < now)
            throw new RuleException("IN_PAST", "a lesson that has started cannot be changed",
                $"lesson started {booking.StartsAt:yyyy-MM-dd HH:mm}");
        return booking;
    }

    private static void CheckSlot(DateTime start, int durationMin, DateTime now)
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

    // Lock the tutor row so two concurrent bookings cannot both pass 6-a-day.
    private async Task CheckTutorDay(string tutorId, DateTime date, Guid lessonId, string? excludeBookingId)
    {
        await db.Database.ExecuteSqlAsync($"SELECT id FROM tutors WHERE id = {tutorId} FOR UPDATE");
        var ids = await Active
            .Where(b => b.TutorId == tutorId && b.StartsAt.Date == date.Date && b.Id != excludeBookingId)
            .Select(b => b.LessonId).Distinct().ToListAsync();
        if (!ids.Contains(lessonId) && ids.Count >= Rules.MaxLessonsPerDay)
            throw new RuleException("TUTOR_DAY_FULL", "at most 6 lessons per tutor per day",
                $"tutor already has {ids.Count} lessons on {date:yyyy-MM-dd}");
    }

    private async Task<string> NextId() =>
        $"L{await db.Database.SqlQuery<long>($"SELECT nextval('booking_id_seq') AS \"Value\"").SingleAsync():D3}";

    private async Task SaveOrTranslate(string student, string tutorId, string roomId)
    {
        try { await db.SaveChangesAsync(); }
        catch (DbUpdateException ex) { throw Translate(ex, student, tutorId, roomId); }
    }

    private static RuleException Translate(DbUpdateException ex, string student,
        string tutorId, string roomId)
    {
        if (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.ExclusionViolation } pg
            && Rules.ConstraintInfo(pg.ConstraintName ?? "") is { } info)
                return new RuleException(info.Code, info.Rule, info.Code switch
                {
                    "STUDENT_CLASH" => $"{student} already has a lesson at that time",
                    "TUTOR_CLASH" => $"tutor {tutorId} is busy at that time",
                    _ => $"room {roomId} is taken at that time",
                });
        throw ex;
    }
}

using BrightPath.Api;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace BrightPath.Tests;

[Collection("pg")]
public class SchemaTests
{
    private readonly PostgresFixture _fx;
    public SchemaTests(PostgresFixture fx) => _fx = fx;

    private static Booking Mk(string id, string student, string tutor, string room,
        DateTime start, int dur = 60, Guid? lesson = null, string status = "booked") => new()
    {
        Id = id, StudentName = student, TutorId = tutor, RoomId = room,
        StartsAt = start, DurationMin = dur, LessonId = lesson ?? Guid.NewGuid(),
        Status = status,
    };

    private async Task<BrightPathDb> DbWithTutors()
    {
        var db = _fx.CreateDb();
        if (!await db.Tutors.AnyAsync())
        {
            db.Tutors.AddRange(
                new Tutor { Id = "T1", Name = "A", Subject = "Maths" },
                new Tutor { Id = "T2", Name = "B", Subject = "English" });
            await db.SaveChangesAsync();
        }
        return db;
    }

    private static string? ConstraintOf(DbUpdateException ex) =>
        (ex.InnerException as PostgresException)?.ConstraintName;

    private static void Assert23P01(DbUpdateException ex) =>
        Assert.Equal("23P01", (ex.InnerException as PostgresException)?.SqlState);

    [Fact]
    public async Task OverlappingStudentIsRejected()
    {
        await using var db = await DbWithTutors();
        var t = new DateTime(2026, 3, 6, 10, 0, 0);
        db.Bookings.Add(Mk("L901", "STU", "T1", "R1", t));
        db.Bookings.Add(Mk("L902", "STU", "T2", "R2", t.AddMinutes(30)));
        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        Assert23P01(ex);
        Assert.Equal("no_student_clash", ConstraintOf(ex));
    }

    [Fact]
    public async Task OverlappingTutorInDifferentRoomIsRejected()
    {
        await using var db = await DbWithTutors();
        var t = new DateTime(2026, 3, 6, 11, 0, 0);
        db.Bookings.Add(Mk("L903", "TU1", "T1", "R1", t));
        db.Bookings.Add(Mk("L904", "TU2", "T1", "R2", t.AddMinutes(30)));
        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        Assert23P01(ex);
        Assert.Equal("no_tutor_clash", ConstraintOf(ex));
    }

    [Fact]
    public async Task OverlappingRoomIsRejected()
    {
        await using var db = await DbWithTutors();
        var t = new DateTime(2026, 3, 6, 12, 0, 0);
        db.Bookings.Add(Mk("L905", "RO1", "T1", "R1", t));
        db.Bookings.Add(Mk("L906", "RO2", "T2", "R1", t.AddMinutes(30)));
        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        Assert23P01(ex);
        Assert.Equal("no_room_clash", ConstraintOf(ex));
    }

    [Fact]
    public async Task ExamPairSharingLessonIdAndRoomPasses()
    {
        await using var db = await DbWithTutors();
        var lesson = Guid.NewGuid();
        var t = new DateTime(2026, 3, 6, 14, 0, 0);
        db.Bookings.Add(Mk("L907", "PR1", "T1", "R3", t, lesson: lesson));
        db.Bookings.Add(Mk("L908", "PR2", "T1", "R3", t, lesson: lesson));
        await db.SaveChangesAsync();
        Assert.Equal(2, await db.Bookings.CountAsync());
    }

    [Fact]
    public async Task NoShowStillBlocksTheRoom()
    {
        await using var db = await DbWithTutors();
        var t = new DateTime(2026, 3, 6, 13, 0, 0);
        db.Bookings.Add(Mk("L909", "NS1", "T1", "R1", t, status: "no_show"));
        db.Bookings.Add(Mk("L910", "NS2", "T2", "R1", t.AddMinutes(30)));
        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        Assert.Equal("no_room_clash", ConstraintOf(ex));
    }

    [Fact]
    public async Task CancelledFreesTheRoom()
    {
        await using var db = await DbWithTutors();
        var t = new DateTime(2026, 3, 6, 15, 0, 0);
        var gone = Mk("L911", "CX1", "T1", "R4", t, status: "cancelled");
        gone.CancelledAt = t;
        gone.CancelledBy = "family";
        db.Bookings.Add(gone);
        db.Bookings.Add(Mk("L912", "CX2", "T2", "R4", t));
        await db.SaveChangesAsync();
    }
}

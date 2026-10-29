using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace BrightPath.Api;

// Loads the export exactly as given, flags the two rows that broke a rule,
// and prints the import report (DECISIONS section 3).
public static class Seed
{
    private record Row(string Id, DateTime Start, int Duration, string Student,
        string TutorId, string RoomId, string Status, DateTime? CancelledAt, string Note);

    public static async Task<string> RunAsync(IServiceProvider sp, string seedDir)
    {
        using var scope = sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BrightPathDb>();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();
        await db.Database.EnsureDeletedAsync();
        await db.Database.MigrateAsync();
        return await Import(db, seedDir, clock.Now);
    }

    public static async Task<string> Import(BrightPathDb db, string seedDir, DateTime now)
    {
        var report = new List<string>();
        report.Add("Bright Path import report");
        report.Add("heuristic: cancelled_by = 'centre' when the note mentions the tutor, else 'family'");
        report.Add("");

        var tutors = File.ReadAllLines(Path.Combine(seedDir, "tutors.csv"))
            .Skip(1).Select(l => l.Split(','))
            .Select(p => new Tutor { Id = p[0], Name = p[1], Subject = p[2] });
        db.Tutors.AddRange(tutors);
        await db.SaveChangesAsync();

        var rows = File.ReadAllLines(Path.Combine(seedDir, "lessons_export.csv"))
            .Skip(1).Where(l => l.Length > 0).Select(l => l.Split(',')).Select(p => new Row(
                p[0], DateTime.Parse($"{p[1]} {p[2]}"), int.Parse(p[3]), p[4], p[5], p[6],
                p[7],
                string.IsNullOrWhiteSpace(p[8]) ? null : DateTimeOffset.Parse(p[8]).DateTime,
                p.Length > 9 ? p[9] : "")).ToList();

        // Rows that share tutor+room+date+start are one lesson (exam pair).
        var lessonIds = new Dictionary<string, Guid>();
        Guid LessonFor(Row r)
        {
            var k = $"{r.TutorId}|{r.RoomId}|{r.Start:o}|{r.Duration}";
            if (!lessonIds.TryGetValue(k, out var g)) lessonIds[k] = g = Guid.NewGuid();
            return g;
        }

        bool Overlap(Row a, Row b) =>
            a.Start < b.Start.AddMinutes(b.Duration) && b.Start < a.Start.AddMinutes(a.Duration);

        // Flag history clashes in file order: the later-added row carries the flag.
        var flags = new Dictionary<string, string>();
        var occupied = rows.Where(r => r.Status != BookingStatus.Cancelled).ToList();
        for (var i = 0; i < occupied.Count; i++)
            for (var j = 0; j < i; j++)
            {
                var (later, earlier) = (occupied[i], occupied[j]);
                if (later.Start.Date != earlier.Start.Date || !Overlap(later, earlier)) continue;
                if (later.Student == earlier.Student)
                    flags[later.Id] = "student_clash";
                else if (later.TutorId == earlier.TutorId && later.RoomId != earlier.RoomId
                         && LessonFor(later) != LessonFor(earlier))
                    flags.TryAdd(later.Id, "tutor_clash");
            }

        var bookings = rows.Select(r => new Booking
        {
            Id = r.Id, LessonId = LessonFor(r), StudentName = r.Student.Trim(),
            TutorId = r.TutorId, RoomId = r.RoomId, StartsAt = r.Start,
            DurationMin = r.Duration, Status = r.Status,
            CancelledAt = r.CancelledAt,
            CancelledBy = r.Status == BookingStatus.Cancelled
                ? (r.Note.Contains("tutor", StringComparison.OrdinalIgnoreCase) ? "centre" : "family")
                : null,
            ImportedClash = flags.GetValueOrDefault(r.Id),
        }).ToList();

        try
        {
            db.Bookings.AddRange(bookings);
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException pg)
        {
            report.Add($"STOP: unexpected constraint hit {pg.ConstraintName}: {pg.MessageText}");
            return string.Join('\n', report);
        }

        foreach (var b in bookings.Where(b => b.Status == BookingStatus.Cancelled))
            db.BookingChanges.Add(new BookingChange
            {
                BookingId = b.Id, Kind = "cancelled",
                OldStartsAt = b.StartsAt, OldRoomId = b.RoomId,
                ChangedAt = b.CancelledAt!.Value,
                AfterCutoff = Rules.AfterCutoff(b.StartsAt, b.CancelledAt.Value),
                Late = Rules.Late(b.StartsAt, b.CancelledAt.Value, b.CancelledBy!),
            });
        await db.SaveChangesAsync();

        report.Add($"loaded {bookings.Count} rows, {lessonIds.Count} distinct lessons");
        foreach (var b in bookings.Where(b => b.ImportedClash == "student_clash"))
            report.Add($"{b.Id}: student clash with an earlier booking ({b.StartsAt:yyyy-MM-dd}, past)");
        foreach (var b in bookings.Where(b => b.ImportedClash == "tutor_clash"))
            report.Add($"{b.Id}: tutor clash ({b.StartsAt:yyyy-MM-dd})" +
                (b.StartsAt > now ? " - FUTURE, receptionist must fix" : ""));
        foreach (var b in bookings.Where(b => b.StartsAt.DayOfWeek == DayOfWeek.Monday))
            report.Add($"{b.Id}: booked on a Monday ({b.StartsAt:yyyy-MM-dd})" +
                (b.StartsAt > now ? " - FUTURE, receptionist must fix" : ""));
        foreach (var g in bookings.Where(b => b.Status != BookingStatus.Cancelled)
                     .GroupBy(b => (b.TutorId, b.StartsAt.Date)).Where(g => g.Select(b => b.LessonId).Distinct().Count() > 6))
            report.Add($"{g.Key.TutorId}: {g.Select(b => b.LessonId).Distinct().Count()} lessons on {g.Key.Date:yyyy-MM-dd} (over the 6-a-day limit)");
        foreach (var g in bookings.GroupBy(b => b.LessonId).Where(g => g.Count() > 1))
            report.Add($"exam pair {g.Key}: {string.Join(" + ", g.Select(b => b.Id))}");
        return string.Join('\n', report);
    }
}


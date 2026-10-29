namespace BrightPath.Api;

// The code-side rules from DECISIONS section 3: everything that depends on
// "now", on counting, or that the centre may want to bend later lives here.
public static class Rules
{
    public static bool ClosedDay(DateTime startsAt) => startsAt.DayOfWeek == DayOfWeek.Monday;

    // Lessons run 09:00 to 21:30 (A5: opening hours are what the export shows).
    public static bool OutsideHours(DateTime startsAt, int durationMin)
    {
        var end = startsAt.AddMinutes(durationMin);
        return startsAt.TimeOfDay < new TimeSpan(9, 0, 0)
            || end.TimeOfDay > new TimeSpan(21, 30, 0)
            || end.Date != startsAt.Date;
    }

    public static bool InPast(DateTime startsAt, DateTime now) => startsAt < now;

    public static DateTime Cutoff(DateTime startsAt) => startsAt.Date.AddDays(-1).AddHours(16);

    public static bool AfterCutoff(DateTime startsAt, DateTime now) => now > Cutoff(startsAt);

    public static bool Late(DateTime startsAt, DateTime now, string cancelledBy) =>
        cancelledBy == "family" && (startsAt - now) < TimeSpan.FromHours(4);

    // Postgres exclusion-constraint name -> error code + the rule in plain English.
    public static (string Code, string Rule)? ConstraintInfo(string constraintName) =>
        constraintName switch
        {
            "no_student_clash" => ("STUDENT_CLASH", "a student cannot be in two lessons at once"),
            "no_tutor_clash" => ("TUTOR_CLASH", "a tutor can only be in one room at a time"),
            "no_room_clash" => ("ROOM_CLASH", "a room holds one lesson at a time"),
            _ => null,
        };
}

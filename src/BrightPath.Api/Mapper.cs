namespace BrightPath.Api;

public static class Mapper
{
    public static BookingResponse ToResponse(this Booking b) =>
        new(b.Id, b.LessonId, b.StudentName, b.TutorId, b.RoomId, b.StartsAt,
            b.DurationMin, b.Status, b.CancelledAt, b.CancelledBy, b.ImportedClash);

    public static ChangeResponse ToResponse(this BookingChange c) =>
        new(c.Id, c.BookingId, c.Kind, c.OldStartsAt, c.OldRoomId,
            c.NewStartsAt, c.NewRoomId, c.ChangedAt, c.AfterCutoff, c.Late);
}

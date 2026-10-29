namespace BrightPath.Api;

public record BookingResponse(
    string Id, Guid LessonId, string StudentName, string TutorId, string RoomId,
    DateTime StartsAt, int DurationMin, string Status,
    DateTime? CancelledAt, string? CancelledBy, string? ImportedClash);

public record ChangeResponse(
    long Id, string BookingId, string Kind,
    DateTime? OldStartsAt, string? OldRoomId, DateTime? NewStartsAt, string? NewRoomId,
    DateTime ChangedAt, bool AfterCutoff, bool Late);

public record ChangeResult(BookingResponse Booking, ChangeResponse Change);

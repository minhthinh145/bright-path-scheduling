namespace BrightPath.Api;

public record CreateBookingRequest(string? StudentName, string? TutorId, string? RoomId,
    DateTime? StartsAt, int? DurationMin, Guid? JoinLessonId);
public record MoveRequest(DateTime? StartsAt, string? RoomId);
public record CancelRequest(string? CancelledBy);

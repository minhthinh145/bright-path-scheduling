using System.ComponentModel.DataAnnotations.Schema;

namespace BrightPath.Api;

public class Tutor
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Subject { get; set; } = "";
}

public class Room
{
    public string Id { get; set; } = "";
}

public class Booking
{
    public string Id { get; set; } = "";
    public Guid LessonId { get; set; }
    public string StudentName { get; set; } = "";
    public string TutorId { get; set; } = "";
    public string RoomId { get; set; } = "";
    public DateTime StartsAt { get; set; }
    public int DurationMin { get; set; }
    public string Status { get; set; } = "booked";
    public DateTime? CancelledAt { get; set; }
    public string? CancelledBy { get; set; }
    public string? ImportedClash { get; set; }
}

public class BookingChange
{
    public long Id { get; set; }
    public string BookingId { get; set; } = "";
    public string Kind { get; set; } = "";
    public DateTime? OldStartsAt { get; set; }
    public string? OldRoomId { get; set; }
    public DateTime? NewStartsAt { get; set; }
    public string? NewRoomId { get; set; }
    public DateTime ChangedAt { get; set; }
    public bool AfterCutoff { get; set; }
    public bool Late { get; set; }
}

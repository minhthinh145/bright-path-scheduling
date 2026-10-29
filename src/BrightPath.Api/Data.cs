using Microsoft.EntityFrameworkCore;

namespace BrightPath.Api;

public class BrightPathDb : DbContext
{
    public BrightPathDb(DbContextOptions<BrightPathDb> options) : base(options) { }

    public DbSet<Tutor> Tutors => Set<Tutor>();
    public DbSet<Room> Rooms => Set<Room>();
    public DbSet<Booking> Bookings => Set<Booking>();
    public DbSet<BookingChange> BookingChanges => Set<BookingChange>();

    protected override void OnModelCreating(ModelBuilder mb)
    {
        mb.Entity<Tutor>(e => { e.HasKey(t => t.Id); e.ToTable("tutors"); });
        mb.Entity<Room>(e => { e.HasKey(r => r.Id); e.ToTable("rooms"); });
        mb.Entity<Booking>(e =>
        {
            e.HasKey(b => b.Id);
            e.Property(b => b.StartsAt).HasColumnType("timestamp");
            e.Property(b => b.CancelledAt).HasColumnType("timestamp");
            e.HasOne<Tutor>().WithMany().HasForeignKey(b => b.TutorId);
            e.HasOne<Room>().WithMany().HasForeignKey(b => b.RoomId);
            e.ToTable("bookings", t =>
            {
                t.HasCheckConstraint("CK_bookings_duration", "duration_min IN (60, 90)");
                t.HasCheckConstraint("CK_bookings_status", "status IN ('booked','cancelled','no_show')");
                t.HasCheckConstraint("CK_bookings_cancelled_by", "cancelled_by IS NULL OR cancelled_by IN ('family','centre')");
                t.HasCheckConstraint("CK_bookings_imported_clash", "imported_clash IS NULL OR imported_clash IN ('student_clash','tutor_clash')");
                t.HasCheckConstraint("CK_bookings_cancel_pair", "(status = 'cancelled') = (cancelled_at IS NOT NULL)");
            });
        });
        mb.Entity<BookingChange>(e =>
        {
            e.HasKey(c => c.Id);
            e.Property(c => c.OldStartsAt).HasColumnType("timestamp");
            e.Property(c => c.NewStartsAt).HasColumnType("timestamp");
            e.Property(c => c.ChangedAt).HasColumnType("timestamp");
            e.HasOne<Booking>().WithMany().HasForeignKey(c => c.BookingId);
            e.ToTable("booking_changes", t =>
                t.HasCheckConstraint("CK_booking_changes_kind", "kind IN ('added','moved','cancelled')"));
        });
    }
}

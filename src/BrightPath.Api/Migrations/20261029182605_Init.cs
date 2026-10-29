using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace BrightPath.Api.Migrations
{
    /// <inheritdoc />
    public partial class Init : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "rooms",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_rooms", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "tutors",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    subject = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tutors", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "bookings",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    lesson_id = table.Column<Guid>(type: "uuid", nullable: false),
                    student_name = table.Column<string>(type: "text", nullable: false),
                    tutor_id = table.Column<string>(type: "text", nullable: false),
                    room_id = table.Column<string>(type: "text", nullable: false),
                    starts_at = table.Column<DateTime>(type: "timestamp", nullable: false),
                    duration_min = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    cancelled_at = table.Column<DateTime>(type: "timestamp", nullable: true),
                    cancelled_by = table.Column<string>(type: "text", nullable: true),
                    imported_clash = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_bookings", x => x.id);
                    table.CheckConstraint("CK_bookings_cancel_pair", "(status = 'cancelled') = (cancelled_at IS NOT NULL)");
                    table.CheckConstraint("CK_bookings_cancelled_by", "cancelled_by IS NULL OR cancelled_by IN ('family','centre')");
                    table.CheckConstraint("CK_bookings_duration", "duration_min IN (60, 90)");
                    table.CheckConstraint("CK_bookings_imported_clash", "imported_clash IS NULL OR imported_clash IN ('student_clash','tutor_clash')");
                    table.CheckConstraint("CK_bookings_status", "status IN ('booked','cancelled','no_show')");
                    table.ForeignKey(
                        name: "fk_bookings_rooms_room_id",
                        column: x => x.room_id,
                        principalTable: "rooms",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_bookings_tutors_tutor_id",
                        column: x => x.tutor_id,
                        principalTable: "tutors",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "booking_changes",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    booking_id = table.Column<string>(type: "text", nullable: false),
                    kind = table.Column<string>(type: "text", nullable: false),
                    old_starts_at = table.Column<DateTime>(type: "timestamp", nullable: true),
                    old_room_id = table.Column<string>(type: "text", nullable: true),
                    new_starts_at = table.Column<DateTime>(type: "timestamp", nullable: true),
                    new_room_id = table.Column<string>(type: "text", nullable: true),
                    changed_at = table.Column<DateTime>(type: "timestamp", nullable: false),
                    after_cutoff = table.Column<bool>(type: "boolean", nullable: false),
                    late = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_booking_changes", x => x.id);
                    table.CheckConstraint("CK_booking_changes_kind", "kind IN ('added','moved','cancelled')");
                    table.ForeignKey(
                        name: "fk_booking_changes_bookings_booking_id",
                        column: x => x.booking_id,
                        principalTable: "bookings",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_booking_changes_booking_id",
                table: "booking_changes",
                column: "booking_id");

            migrationBuilder.CreateIndex(
                name: "ix_bookings_room_id",
                table: "bookings",
                column: "room_id");

            migrationBuilder.CreateIndex(
                name: "ix_bookings_tutor_id",
                table: "bookings",
                column: "tutor_id");

            // DECISIONS section 3: the clash rules live in the database.
            migrationBuilder.Sql("""
                CREATE EXTENSION IF NOT EXISTS btree_gist;

                ALTER TABLE bookings
                    ADD COLUMN period tsrange
                    GENERATED ALWAYS AS (tsrange(starts_at, starts_at + duration_min * interval '1 minute'))
                    STORED;

                ALTER TABLE bookings ADD CONSTRAINT no_student_clash
                    EXCLUDE USING gist (student_name WITH =, period WITH &&)
                    WHERE (status <> 'cancelled' AND imported_clash IS DISTINCT FROM 'student_clash');

                ALTER TABLE bookings ADD CONSTRAINT no_tutor_clash
                    EXCLUDE USING gist (tutor_id WITH =, period WITH &&, lesson_id WITH <>)
                    WHERE (status <> 'cancelled' AND imported_clash IS DISTINCT FROM 'tutor_clash');

                ALTER TABLE bookings ADD CONSTRAINT no_room_clash
                    EXCLUDE USING gist (room_id WITH =, period WITH &&, lesson_id WITH <>)
                    WHERE (status <> 'cancelled');

                CREATE SEQUENCE booking_id_seq START 35;
                """);

            migrationBuilder.InsertData(
                table: "rooms",
                column: "id",
                values: new object[] { "R1", "R2", "R3", "R4", "R5", "R6" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "booking_changes");

            migrationBuilder.DropTable(
                name: "bookings");

            migrationBuilder.DropTable(
                name: "rooms");

            migrationBuilder.DropTable(
                name: "tutors");

            migrationBuilder.Sql("DROP SEQUENCE IF EXISTS booking_id_seq; DROP EXTENSION IF EXISTS btree_gist;");
        }
    }
}

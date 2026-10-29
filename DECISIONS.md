# Decisions — Bright Path Learning Centre

Pinned "now": **Thursday 2026-03-05 10:00** (inside the export week, 03–10 March). The tool never reads the real clock.

## 1. Reading the situation

### What actually hurts (vs. merely mentioned)
- **PAIN (top — the owner's "never") — a student booked into two places at once.** Owner: "Twice last term … That can never happen again — if the system allows it, the system is broken." The export shows it too (L007/L008).
- **PAIN — late changes do not reach the tutor.** Receptionist: "Someone messages at night, I fix the sheet in the morning, and by then the tutor has already left home." Tutor: "I have driven in for a lesson that was called off the night before. Twice."
- **PAIN — no single source of truth.** Tutor: "I get my day in a message, then a correction, then sometimes a third message. I do not always know which one is real."
- **PAIN (deadline) — only the receptionist understands the sheet**, and she goes on leave in eight weeks. The rules must move from her head into the tool.
- MENTION — owner wants to "open the laptop and see today. Not scroll."
- MENTION — telling the tutor's next family that a slot opened ("tries to remember").
- MENTION — exam pairs: they already work and families like them; the tool must support them, not fix them.

### Questions for the owner (and how each answer changes the build)

**Q1. When a lesson changes after the tutor was told, how must the tutor be reached?**
- **Solution A: Look at the tool.** The tutor's day page shows "changed since you were told". Cheap, but does not reach a tutor already on the way.
- **Solution B: The tool sends a message.** WhatsApp/SMS/push goes to the tutor automatically. Reaches him on the road; needs a messaging provider and tutors' consent.
- **Solution C: The tool tells the receptionist who to call.** Every late change stays "not yet confirmed by tutor" until he taps "seen"; the receptionist gets a list of tutors still to reach and calls them.
- **Until answered:** Solution C — it works with WhatsApp/phone as they are today and makes sure no tutor is forgotten.

**Q2. After 16:00, can the receptionist still add a new lesson for tomorrow?**
- **Solution A: Only cancel or move.** The tool stops her from adding a lesson for tomorrow after 16:00.
- **Solution B: Allow adding, mark it.** The tool lets her add it but marks it as a late change, not confirmed by the tutor until he taps "seen".
- **Until answered:** Solution B — the brief says changes after the cut-off "still happen".

**Q3. When a family moves a lesson, what is it?**
- **Solution A: Same lesson, new time.** The tutor sees one item "moved from Sun 10:00 to Tue 10:00"; a move is not charged.
- **Solution B: Cancel + new booking.** The tutor sees two items (cancelled, added); a late move is charged like a late cancellation.
- **Until answered:** Solution A.

**Q4. Exam pairs: how many students, and how do they count toward the six-a-day limit?**
- **Solution A: Max two, counts as one.** A second student can join a lesson, never a third; the tutor can still teach six slots.
- **Solution B: Max two, counts as two.** A tutor with three pairs is full for the day; the tool blocks a seventh student.
- **Until answered:** Solution A (one tutor, one room, one slot).

**Q5. The six-a-day limit: block or allow an override?**
- **Solution A: Block.** The tool refuses the seventh booking; the receptionist must find another tutor.
- **Solution B: Override with a reason.** The tool warns, the receptionist confirms and types why, and the owner can see every time it happened.
- **Until answered:** Solution A — the owner "wants it enforced".

**Q6. No-show: does the family pay, and is the tutor paid?**
- **Solution A: Family pays, tutor paid.** A no-show is recorded like a lesson that happened; billing does not change.
- **Solution B: Family does not pay.** No-shows are tracked separately for the bill; the owner may want a no-show count per family.
- **Until answered:** Solution A — the room and slot were held for them.

### Personal data and who may do what
- **Personal data:** student names (most are likely children), tutor names and phones, free-text notes.
- **Real risk for this build:** student names in URLs or logs. F1 keeps them in request bodies only and never logs them. The tutors' phone column is not loaded.
- **Who may do what (minimum for F1):** the receptionist books, moves and cancels; tutors and the owner only read. No login in F1 — written under "What I leave broken".

### Where the brief argues with itself (and the reading I chose)

**C1. "Lessons are one-to-one" and "a room holds one lesson at a time" vs. exam pairs (two students, one tutor, one room, same slot, "most weeks").**
- **Reading I chose:** a pair is *one lesson with two students*, not two lessons. One-to-one is the default; a second student may join, never a third.

**C2. "No tutor may be given more than 6 bookings a day — the owner wants it enforced" vs. "the receptionist breaks this rule when she is desperate".**
- **Reading I chose:** the owner decides, so the tool enforces it — a seventh booking is refused (see Q5).

**C3. "The system must never allow a student in two places at once" vs. the export, which contains exactly that (L007/L008), plus a tutor in two rooms (L033/L034), 7 bookings for T1 on 06/03, and a lesson on a Monday (L032).**
- **Reading I chose:** the export is history — what really happened. It is loaded as it is and the rule breaks are reported, not silently fixed. The rules apply to every new booking and every change from now on.

**C4. "Family confirmed" by phone (L008) vs. "that can never happen again".**
- **Reading I chose:** no one can override a double booking — not the receptionist, not a family's confirmation.

**C5. "Tomorrow's schedule is final at 16:00" vs. "changes after the cut-off still happen".**
- **Reading I chose:** "final" means *the version the tutor was told*, not *frozen*. Later changes are allowed but kept as visible changes on top of that version, never written over it.

**C6. "12 tutors and six rooms" vs. the export, which only uses T1–T3 and R1–R3.**
- **Reading I chose:** the export is an anonymised sample of one week. The tool still has six rooms; only the three tutors in tutors.csv are loaded.

### Assumptions I invented
- **A1. Exactly 4 hours is free.** "Up to 4 hours before" includes the 4-hour mark; 3h59 before is charged in full.
- **A2. The 4-hour rule is only for families.** When the tutor or the centre cancels (L017, "tutor sick", 1h20 before), the family is not charged.
- **A3. "Told" means the 16:00 version.** For any lesson, what the tutor was told is the schedule at 16:00 the day before. So a same-day cancellation (L005, cancelled 08:15 for 14:00) is a change after the cut-off.
- **A4. Cancelled lessons don't count toward the six-a-day limit; no-shows do.** Cancelling frees the slot; a no-show frees neither.
- **A5. Opening hours are what the export shows:** Tuesday to Sunday, 09:00 to 21:30.
- **A6. Pinned "today": Thursday 2026-03-05,** inside the export week. The tool uses this date, not the real clock.
- **A7. Who cancelled in the export:** a cancellation whose note mentions the tutor (L017 "tutor sick") is the centre's; otherwise the family's.

## 2. Choosing what to build

### Features this tool needs
- **F1. Booking with rules:** the receptionist books, cancels or moves a lesson. The tool blocks any booking that puts a student in two lessons at once, a tutor in two rooms at once, or two lessons in one room. It also blocks Mondays and a seventh lesson for a tutor in one day. Exam pairs of two students are allowed. Changes made after 16:00 are saved as changes.
- **F2. Tutor's day with changes:** each tutor sees the schedule he was given at 16:00, plus every change since then. He taps "seen" to confirm. The receptionist sees which tutors have not confirmed yet, so she knows who to call.
- **F3. Today on one screen:** the owner opens the laptop and sees all of today's lessons, by room and hour, without scrolling.
- **F4. Late-cancellation charges:** the tool applies the 4-hour rule: who pays, and whether the tutor is paid.
- **F5. Free-slot offer:** when a lesson is cancelled, the tool suggests a family who could take the free slot.
- **F6. Automatic messages:** the tool sends the tutor a WhatsApp or SMS when his day changes.

### The one I build: F1 — Booking with rules
- **It is the heart of the system.** It is the only place where a booking is made or changed, so it is the only place that can guarantee the owner's rule: a student in two places at once "can never happen again — if the system allows it, the system is broken."
- **The other features depend on it.** F2, F3 and F5 all show or use bookings. If bookings can still clash, those screens show wrong information.
- **It keeps what the tutor was told.** Late changes are saved as changes, not written over. That makes F2 a small next step.
- **It takes the rules out of the receptionist's head.** In eight weeks she is on leave; whoever sits at the desk next gets a tool that refuses a clash instead of a sheet that allows one.
- **It fits in the time.** A few clear rules, each easy to test, using the rule breaks already in the export.

### What I leave broken
- Tutors still hear about late changes from the receptionist on WhatsApp. "I drove in for a cancelled lesson" can still happen until F2 is built.
- No screen and no login: the receptionist uses the API through Swagger. No one-screen view for the owner, no charges, no free-slot offers, no automatic messages.
- No no-show endpoint (cut for time). No-show rows from the export still hold their room and slot.
- A cancellation sent at night still waits until the receptionist reads it in the morning (today the family messages the receptionist; letting families cancel in the tool would close this gap).

## 3. Design of F1

Stack: ASP.NET Core Web API + EF Core + PostgreSQL. No frontend.

### Data model
One row per student per lesson — the same shape as the export. An exam pair is two rows sharing one `lesson_id`.

```text
tutors           (id 'T1', name, subject)            -- phone column not loaded
rooms            (id 'R1'..'R6')
bookings         (id 'L001',                         -- one student in one lesson
                  lesson_id,                         -- shared by the two students of an exam pair
                  student_name, tutor_id, room_id,
                  starts_at, duration_min,           -- CHECK duration_min IN (60, 90); end is calculated
                  status,                            -- booked | cancelled | no_show
                  cancelled_at, cancelled_by,        -- family | centre
                  imported_clash)                    -- null, or student_clash / tutor_clash for history rows that broke that rule
booking_changes  (id, booking_id, kind,              -- added | moved | cancelled
                  old_starts_at, old_room_id, new_starts_at, new_room_id,
                  changed_at, after_cutoff, late)    -- append-only, never updated
```

- `bookings.period` is a generated start-to-end time range; the three clash rules compare it with "overlaps".
- New booking ids continue the export's style from L035.
- `lesson_id` is a generated id; a student who moves out of an exam pair gets a fresh one.
- Student names are trimmed before saving, so "Le Minh Chau " and "Le Minh Chau" are the same student.

### A booking cancelled or moved after the tutor was told
- **Cancel:** the row stays, `status = cancelled`, with `cancelled_at` and `cancelled_by`. Never deleted. Every change gets a `booking_changes` row; `after_cutoff = true` if it was made after 16:00 the day before.
- **Move:** same booking, same id, new time or room; the change row keeps the old time and room. A move is per student: if one student of a pair moves, they leave the pair and get a new `lesson_id`. Moving a whole pair at once is not in F1.
- **Added after 16:00:** a new booking plus a change row `added`.
- The tutor's 16:00 version is never overwritten; changes are only added on top. F1 writes the change rows; showing the "as told" version to the tutor is F2.
- **History from the export:** loaded as it is. L008 (student clash with L007, "added by phone") gets `imported_clash = student_clash`, and L034 (tutor clash with L033) gets `tutor_clash`. Each flag only lifts *the rule that row broke*: L034 still blocks room R2 on 10/03 09:00 for anyone else. L034 is after the pinned "now", so the import report lists it as **a future clash the receptionist must fix**. L032 (Monday) and T1's seventh lesson on 06/03 are listed in the import report. L005 and L017 get `cancelled` change rows from their `cancelled_at`.

### Which rules live in the database, which in code — and why

| Rule | Where | How |
|---|---|---|
| A student is never in two lessons at once | **Database** | Exclusion constraint: same student, overlapping time → refused |
| A tutor is in one room at a time | **Database** | Exclusion constraint: same tutor, overlapping time, different lesson → refused |
| A room holds one lesson at a time | **Database** | Exclusion constraint: same room, overlapping time, different lesson → refused (an exam pair shares one lesson, so it passes) |
| Lessons are 60 or 90 minutes; status is booked / cancelled / no_show; a cancelled booking has a cancel time | **Database** | CHECK constraints |
| Tutors and rooms must exist (six rooms) | **Database** | Foreign keys |
| Closed on Monday; lessons between 09:00 and 21:30 | Code | Checked when booking or moving |
| No more than 6 lessons per tutor per day | Code | Counts distinct `lesson_id` (a pair = one lesson), not cancelled, inside the same transaction as the insert, with that tutor's day locked |
| An exam pair has at most 2 students, same tutor, room and time | Code | Checked when a student joins a lesson |
| A change after 16:00 the day before is marked "after cut-off" | Code | Compared with the pinned "now" |
| A family cancelling less than 4h before is marked "late" | Code | Compared with the pinned "now"; recorded only, billing is not built |
| Only booked → cancelled, booked → no_show | Code | Checked in the cancel / no-show actions |

**Why this split:** the database guards the rules that must never break, whoever writes the data — the three clash rules are the owner's "if the system allows it, the system is broken". Code holds the rules that depend on "now", on counting, or that the owner may want to bend later (Q5). Cancelled bookings are ignored by the clash constraints, and a flagged history row is ignored only by the constraint it broke; no-shows are not, because a no-show frees neither the room nor the slot.

### API

| Endpoint | Does | Answers |
|---|---|---|
| `POST /bookings` | Book a student (optional `joinLessonId` for an exam pair) | 201; 409 `{code, rule, message}` when a business rule refuses it (`STUDENT_CLASH`, `TUTOR_CLASH`, `ROOM_CLASH`, `CLOSED_DAY`, `OUTSIDE_HOURS`, `TUTOR_DAY_FULL`, `PAIR_FULL`); 422 when the input itself is malformed |
| `POST /bookings/{id}/move` | New time and/or room, same booking | 200 + change, or 409 |
| `POST /bookings/{id}/cancel` | `cancelledBy: family / centre` | 200 + change (shows `afterCutoff`, `late`) |
| `GET /bookings?date=&tutorId=` | List one day's bookings | 200 |
| `GET /bookings/{id}/changes` | The change history of a booking | 200 |

**Cut for time:** `POST /bookings/{id}/no-show`. The rule itself still holds — the database keeps a no-show inside the clash checks, so it frees neither the room nor the slot — but marking one goes through a later commit.

**Endpoint I rejected:** `PUT /bookings/{id}` (edit any field). It would let a move, a cancellation or a tutor swap silently overwrite what the tutor was told — exactly what the brief forbids — and it hides *which* change happened. Named actions (`move`, `cancel`) always write a change row.

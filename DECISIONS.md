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

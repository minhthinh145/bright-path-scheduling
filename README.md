# Bright Path scheduling

F1 — Booking with rules, for the Bright Path tutoring centre: an API that books,
moves and cancels lessons while the clash rules (a student is never in two lessons
at once, a tutor in one room at a time, a room holds one lesson at a time) are
guaranteed by Postgres itself. API only — Swagger is at `/swagger`.
The design and the rules are in [DECISIONS.md](DECISIONS.md).

## Prerequisites

- .NET 8 SDK
- Docker (Postgres 16 on host port 5433)

## Run

```sh
docker compose up -d
dotnet test
dotnet run --project src/BrightPath.Api -- seed
dotnet run --project src/BrightPath.Api
```

**The seed command drops and recreates the `brightpath` database**, then loads
the export as given and prints the import report.

The API listens on the port in `launchSettings.json` (`http://localhost:5038`).

## The clock

`now` is pinned at `Clock:Now` = `2026-03-05T10:00:00` (inside the export week),
not the real clock. Override with `Clock__Now=2026-03-06T09:00:00 dotnet run ...`.

## Endpoints

| Endpoint | Does |
|---|---|
| `POST /bookings` | Book a student (`joinLessonId` for an exam pair). Writes an `added` change row. |
| `POST /bookings/{id}/move` | New time and/or room; writes a `moved` change row. |
| `POST /bookings/{id}/cancel` | `cancelledBy: family \| centre`; writes a `cancelled` row with `afterCutoff`, `late`. |
| `GET /bookings?date=YYYY-MM-DD&tutorId=` | One day's bookings (tutor filter optional). |
| `GET /bookings/{id}/changes` | The append-only change history. |

No-show endpoint: not built (cut for time); no-show rows from the export still hold their room and slot.

A refused change answers `409 {"code", "rule", "message"}`:

| Code | Rule |
|---|---|
| `STUDENT_CLASH` | a student cannot be in two lessons at once |
| `TUTOR_CLASH` | a tutor can only be in one room at a time |
| `ROOM_CLASH` | a room holds one lesson at a time |
| `CLOSED_DAY` | the centre is closed on Mondays |
| `OUTSIDE_HOURS` | lessons run 09:00-21:30 |
| `TUTOR_DAY_FULL` | at most 6 lessons per tutor per day |
| `PAIR_FULL` | an exam pair has at most 2 students |
| `IN_PAST` | cannot book in the past (booking); a lesson that has started cannot be changed (move, cancel) |
| `NOT_BOOKED` | only a booked lesson can be changed |

Malformed input answers `422`, an unknown booking id `404`.

## What I saw

`dotnet test`:

```
Passed!  - Failed:     0, Passed:    53, Skipped:     0, Total:    53, Duration: 1 s
```

`dotnet run --project src/BrightPath.Api -- seed`:

```
Bright Path import report
heuristic: cancelled_by = 'centre' when the note mentions the tutor, else 'family'

loaded 34 rows, 33 distinct lessons
L008: student clash with an earlier booking (2026-03-04, past)
L034: tutor clash (2026-03-10) - FUTURE, receptionist must fix
L032: booked on a Monday (2026-03-09) - FUTURE, receptionist must fix
T1: 7 lessons on 2026-03-06 (over the 6-a-day limit)
exam pair aaf307ff-b700-4a00-9051-4cedcb03ff2e: L009 + L010
```

Book a lesson → `201`:

```
{"id":"L035","lessonId":"3140cfc5-a32a-4649-877c-c20fe2a8855b","studentName":"Demo Student","tutorId":"T3","roomId":"R5","startsAt":"2026-03-10T11:00:00","durationMin":60,"status":"booked","cancelledAt":null,"cancelledBy":null,"importedClash":null}
```

Book the same student into an overlapping slot → `409`:

```
{"code":"STUDENT_CLASH","rule":"a student cannot be in two lessons at once","message":"Nguyen Thi Ha already has a lesson at that time"}
```

Cancel L014 (starts 10:30, "now" is 10:00 — inside 4h, past the cut-off) → `200` (shortened):

```
{"booking":{"id":"L014",...,"status":"cancelled","cancelledAt":"2026-03-05T10:00:00","cancelledBy":"family"},"change":{"bookingId":"L014","kind":"cancelled","changedAt":"2026-03-05T10:00:00","afterCutoff":true,"late":true}}
```

## Code layout

- `Program.cs` — app wiring: Postgres, pinned clock, Swagger, endpoints, the `seed` command.
- `Domain.cs` — the four entities (tutor, room, booking, booking_change).
- `Data.cs` — the EF Core DbContext and the CHECK constraints.
- `Rules.cs` — the code-side rules (closed day, hours, cut-off, late) and constraint-name mapping.
- `BookingService.cs` — all business logic; throws `RuleException`/`BadInputException`/`NotFoundException`.
- `Endpoints.cs` — the five routes; turns those exceptions into 409/422/404.
- `Requests.cs` — the request bodies.
- `Errors.cs` — the three exception types.
- `Seed.cs` — loads the export as given, flags history clashes, prints the import report.
- `Migrations/` — the schema, the exclusion constraints, the six rooms.

## Troubleshooting

- **`docker compose up` fails:** start the Docker daemon (or Docker Desktop) first.
- **Port 5433 already in use:** stop the other Postgres, or change the host port in `docker-compose.yml` and the `Port=` in `appsettings.json` to match.
- **First request after seeding fails with `57P01`** while the API was already running: the seed drops the database under the API's connection pool — retry the request, or restart the API.

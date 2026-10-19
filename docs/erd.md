# Entity-relationship diagram

```mermaid
erDiagram
    CONFERENCE ||--o{ SESSION : "has"
    CONFERENCE ||--o{ REGISTRATION : "has"
    SPEAKER |o--o{ SESSION : "optionally speaks at"
    ROOM |o--o{ SESSION : "optionally hosts"
    ATTENDEE ||--o{ REGISTRATION : "makes"
    SESSION ||--o{ SESSION_REGISTRATION : "has"
    ATTENDEE ||--o{ SESSION_REGISTRATION : "makes"

    CONFERENCE {
        int Id PK
        string Title
        string Description
        string Location
        string Venue
        datetime StartDate
        datetime EndDate
        string Status
        int MaxAttendees
        string OrganizerName
        string Website
    }

    SESSION {
        int Id PK
        string Title
        string Description
        datetime StartTime
        datetime EndTime
        int Capacity
        int ConferenceId FK
        int SpeakerId FK "nullable"
        int RoomId FK "nullable"
    }

    SPEAKER {
        int Id PK
        string FirstName
        string LastName
        string Bio
        string Email
    }

    ROOM {
        int Id PK
        string Name
        int Capacity
        string Location
    }

    ATTENDEE {
        int Id PK
        string FirstName
        string LastName
        string Email
    }

    REGISTRATION {
        int Id PK
        datetime RegisteredAt
        string Status
        int ConferenceId FK
        int AttendeeId FK
    }

    SESSION_REGISTRATION {
        int Id PK
        datetime RegisteredAt
        string Status
        int SessionId FK
        int AttendeeId FK
    }
```

Relationship notes:
- `Conference` 1—* `Session` via `Session.ConferenceId` (required — a session always belongs to a conference).
- `Conference` 1—* `Registration` via `Registration.ConferenceId` (required).
- `Speaker` 0/1—* `Session` via `Session.SpeakerId` (optional — a session can have no speaker).
- `Room` 0/1—* `Session` via `Session.RoomId` (optional).
- `Attendee` 1—* `Registration` via `Registration.AttendeeId` (required). Attendees are deduplicated by `Email` in `RegistrationService`, so one `Attendee` can have registrations across multiple conferences.
- `Session` 1—* `SessionRegistration` via `SessionRegistration.SessionId` (required). `Status` is `"CONFIRMED"`, `"CANCELLED"` (soft-delete on cancel), or `"WAITLISTED"` (joined after the session reached capacity); counts/lookups filter on `"CONFIRMED"` for registered totals and `"WAITLISTED"` for the waitlist. Cancelling a `"CONFIRMED"` row promotes the earliest `"WAITLISTED"` row (by `RegisteredAt`, tiebreak `Id`) to `"CONFIRMED"`.
- `Attendee` 1—* `SessionRegistration` via `SessionRegistration.AttendeeId` (required). Attendees are deduplicated by `Email` (trimmed, case-insensitive) in `SessionRegistrationService`.

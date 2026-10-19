# Deferred from code review — 2026-09-15-session-waitlist (2026-09-15)

- **No visual indicator distinguishes a *waitlisted* attendee's own state on `SessionDetailPage`** —
  `frontend/src/pages/SessionDetailPage.jsx` — a confirmed registration shows "You're registered",
  but a waitlisted attendee sees the same "Join Waitlist" button as a first-time visitor, since
  `currentAttendeeRegistrationId` is only populated for `CONFIRMED` rows.
- **`CancelRegistrationAsync`'s promotion path calls `SaveChangesAsync()` twice** —
  `backend/BrownEvents.Api/Services/SessionRegistrationService.cs` (once for the cancellation, once
  for the promotion) where a single call would suffice. A minor efficiency nit, not a correctness
  issue given the guarded transaction wraps both.
- **The new `/api/sessions/{id}/waitlist` route has no dedicated Swagger request example** —
  `backend/BrownEvents.Api/Controllers/SessionsController.cs` — it reuses `/register`'s example by
  virtue of sharing the same request DTO, but there's no explicit example wired to the new route in
  Swagger's route inventory.

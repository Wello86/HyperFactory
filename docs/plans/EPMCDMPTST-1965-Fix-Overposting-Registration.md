# EPMCDMPTST-1965: Fix overposting/mass-assignment in `SessionsController.Register`

## Context

Code review (`mr-comments.md`, Blocking finding #1) found that `POST /api/sessions/{id}/register`
binds `[FromBody] Attendee attendee` directly to the EF entity. Because `Attendee` now has a
`SessionRegistrations` navigation collection (added by this same branch), a client can include a
`sessionRegistrations` array in the JSON body; EF Core's `Add()` graph-fixup inserts those nested
rows as soon as a new `Attendee` is created — **before** `SessionRegistrationService
.RegisterAttendeeAsync`'s `AlreadyRegistered`/`SessionFull` checks even run. This bypasses capacity
enforcement and duplicate-registration prevention entirely, which defeats this ticket's core
purpose. Confirmed exploitable by executing (and then deleting) a throwaway repro against
`AppDbContext`.

Every existing write endpoint in this codebase (`ConferencesController`, `SpeakersController`) has
the same underlying pattern (bind straight to the entity) — there's no existing request-DTO
precedent to mirror, so this fix introduces the first one, scoped only to the endpoint this branch
added. `ConferencesController.Register` has the identical bug but is explicitly out of scope (per
the user's instruction — pre-existing, not introduced by this branch).

Goal: `Register` accepts only the three fields it actually needs (firstName, lastName, email —
exactly what `SessionRegisterModal.jsx` already sends), with no way to smuggle nested entities.

As a secondary benefit, this also closes off `Attendee.Id` overposting (a client-supplied `Id` in
the current request body reaches the EF insert; harmless today since it's an `INSERT` not an
`UPDATE`, but still uncontrolled input reaching a place it shouldn't).

This plan is a distinct follow-up-fix document rather than an edit to the original
`docs/plans/EPMCDMPTST-1965-AttendeesCannotRegister.md` (same branch, but a discrete fix
discovered via code review after the original feature plan was written).

## TDD strategy

**Yes, adapted to a type-narrowing fix.** The vulnerability is a *capability* (an extra bindable
property), not a runtime branch, so there's no failing assertion to write against current code.
Instead: update `SessionRegistrationServiceTests.cs`'s 13 call sites to pass the new DTO type
before the DTO exists — this fails to compile (red), confirming the suite actually exercises the
changed signature. Then implement the DTO + service/controller changes until the suite is green.
Additionally: (a) strengthen `RegisterAttendeeAsync_CreatesNewAttendeeAndRegistration_WhenEmailNotFound`
to assert exact row counts (`Attendees.Count()` == 1, `SessionRegistrations.Count()` == 1), and
(b) add a new test proving the check *ordering* fix — capacity/duplicate checks now run against
actual DB state, not a client-supplied graph — since the DTO having no `SessionRegistrations`
property is a compile-time guarantee the type system already gives us for free, not something a
runtime assertion needs to re-prove.

## Affected files

- `backend/BrownEvents.Api/Dtos/AttendeeRegistrationRequestDto.cs`: **Create**
- `backend/BrownEvents.Api/Services/ISessionRegistrationService.cs`: **Modify**
- `backend/BrownEvents.Api/Services/SessionRegistrationService.cs`: **Modify**
- `backend/BrownEvents.Api/Controllers/SessionsController.cs`: **Modify**
- `backend/BrownEvents.Api/SwaggerExamples/AttendeeRegistrationRequestExample.cs`: **Create**
- `backend/BrownEvents.Tests/SessionRegistrationServiceTests.cs`: **Modify**
- `docs/architecture.md`: **Modify** (§4.10b currently documents the old `Attendee`-bound signature)

## Proposed changes (ordered steps)

1. **New DTO** `Dtos/AttendeeRegistrationRequestDto.cs` — request-only, no `Id`, no nav collections:
   ```csharp
   using System.ComponentModel.DataAnnotations;

   namespace BrownEvents.Api.Dtos;

   public class AttendeeRegistrationRequestDto
   {
       [Required]
       [MaxLength(100)]
       public string FirstName { get; set; } = string.Empty;

       [Required]
       [MaxLength(100)]
       public string LastName { get; set; } = string.Empty;

       [Required]
       [EmailAddress]
       public string Email { get; set; } = string.Empty;
   }
   ```
   Validation attributes mirror `Attendee`'s, so `[ApiController]`'s automatic 400 behavior on
   invalid input is unchanged.

2. **`ISessionRegistrationService.cs`**: change
   `RegisterAttendeeAsync(int sessionId, int capacity, Attendee attendeeData, CancellationToken)`
   to take `AttendeeRegistrationRequestDto attendeeData` instead of `Attendee`. Add
   `using BrownEvents.Api.Dtos;` (keep the existing `using BrownEvents.Api.Models;` — still needed
   for `SessionRegistration`/`Attendee` in the result types).

3. **`SessionRegistrationService.cs`** — `RegisterAttendeeAsync`: keep the existing-attendee lookup
   by trimmed/case-insensitive email unchanged. Replace the "new attendee" branch (currently
   `attendeeData.Email = normalizedEmail; _context.Attendees.Add(attendeeData); ... attendee =
   attendeeData;`) with constructing a fresh `Attendee` entity from the DTO's three fields:
   ```csharp
   if (existingAttendee is null)
   {
       attendee = new Attendee
       {
           FirstName = attendeeData.FirstName,
           LastName = attendeeData.LastName,
           Email = normalizedEmail,
       };
       _context.Attendees.Add(attendee);
       await _context.SaveChangesAsync(cancellationToken);
   }
   else
   {
       attendee = existingAttendee;
   }
   ```
   This is the actual fix: the object handed to `_context.Attendees.Add(...)` is now always a
   locally-constructed `Attendee` with exactly three fields set — there is no path by which
   client-supplied data reaches a navigation collection. Add `using BrownEvents.Api.Dtos;`. The
   existing transaction guard (`_context.Database.IsRelational() ? await
   _context.Database.BeginTransactionAsync() : null`) and the ordering of checks are untouched.

4. **`SessionsController.cs`** — `Register` action: change `[FromBody] Attendee attendee` to
   `[FromBody] AttendeeRegistrationRequestDto request` (rename param since it's no longer an
   `Attendee`; update the one use-site passing it to `RegisterAttendeeAsync` and the XML doc
   `<param>` tag). Change `[SwaggerRequestExample(typeof(Attendee), typeof(AttendeeExample))]` to
   `[SwaggerRequestExample(typeof(AttendeeRegistrationRequestDto), typeof(AttendeeRegistrationRequestExample))]`.
   `using BrownEvents.Api.Dtos;` is already present at the top of this file — no new using needed;
   `using BrownEvents.Api.Models;` stays (still used by `Update`'s `[FromBody] Session session`).

5. **New Swagger example** `SwaggerExamples/AttendeeRegistrationRequestExample.cs` (mirrors
   `AttendeeExample.cs`, which stays untouched/unshared since `ConferencesController.Register` is
   out of scope and still binds `Attendee`):
   ```csharp
   using BrownEvents.Api.Dtos;
   using Swashbuckle.AspNetCore.Filters;

   namespace BrownEvents.Api.SwaggerExamples;

   public class AttendeeRegistrationRequestExample : IExamplesProvider<AttendeeRegistrationRequestDto>
   {
       public AttendeeRegistrationRequestDto GetExamples() => new()
       {
           FirstName = "John",
           LastName = "Smith",
           Email = "john.smith@example.com",
       };
   }
   ```
   No `Program.cs` change needed — Swagger examples are registered via assembly scan
   (`AddSwaggerExamplesFromAssemblyOf<ConferenceExample>()`).

6. **`SessionRegistrationServiceTests.cs`**: change every `new Attendee { FirstName = ..., LastName
   = ..., Email = ... }` literal passed as the 3rd argument to `RegisterAttendeeAsync` (13 call
   sites) to `new AttendeeRegistrationRequestDto { FirstName = ..., LastName = ..., Email = ... }`.
   Leave the one `Attendee` at the "existing attendee" test-setup line (seeded directly via
   `context.Attendees.Add`, never passed to `RegisterAttendeeAsync`) unchanged. Add
   `using BrownEvents.Api.Dtos;` in alphabetical position (`Data`, `Dtos`, `Models`, `Services`).
   Strengthen `RegisterAttendeeAsync_CreatesNewAttendeeAndRegistration_WhenEmailNotFound` to assert
   `context.Attendees.Count() == 1` and `context.SessionRegistrations.Count() == 1` after the call.
   Add a new test — seed a session at capacity, then call `RegisterAttendeeAsync` for a brand-new
   attendee and assert the outcome is `SessionFull` — pinning down that the capacity check runs
   against actual persisted state independent of whatever the request DTO carries.

7. **`docs/architecture.md`** §4.10b: update the `Register` controller signature reference from
   `[FromBody] Attendee attendee` to `[FromBody] AttendeeRegistrationRequestDto request`, and note
   in one sentence why (avoids binding the write endpoint straight to an entity with navigation
   collections). (Checked: `docs/cross-cutting-notes.md` and `docs/testing-ci.md` don't reference
   this signature, so no changes needed there.)

## Scope boundaries & assumptions

- **Out of scope**: `ConferencesController.Register` / `RegistrationService.RegisterAttendeeAsync`
  — identical bug (including via the sibling `Attendee.Registrations` nav property, not just
  `SessionRegistrations`), but explicitly not part of this fix per the user's instruction. Call
  this out explicitly in the MR description as a recommended follow-up ticket, rather than silently
  leaving an equally-exploitable endpoint unmentioned.
- **Out of scope**: frontend changes — `SessionRegisterModal.jsx` → `useSessionRegistration.js` →
  `api.js`'s `registerForSession` already POST exactly `{ firstName, lastName, email }`, which
  matches the new DTO's JSON shape exactly. No frontend file needs to change.
- **Considered and rejected**: `[JsonIgnore]` on `Attendee`'s nav properties, and `[Bind(...)]` on
  the action parameter, as alternatives to a DTO. Both rejected — `[JsonIgnore]` would be a global,
  type-level change affecting every current/future consumer of `Attendee` (and wouldn't produce an
  accurate OpenAPI schema for the endpoint), and `[Bind]` doesn't reliably gate System.Text.Json's
  `[FromBody]` deserialization in .NET 6 and likewise leaves an inaccurate Swagger schema. A
  dedicated request DTO is the standard mitigation and fixes both problems (data exposure and
  schema accuracy) in one move.
- **Naming**: DTO is named `AttendeeRegistrationRequestDto` (not session-specific) so it could be
  adopted by `ConferencesController.Register` later with a one-line signature change, even though
  that reuse isn't wired up in this fix.
- **Assumption**: `AttendeeDto` (the existing response DTO, which includes `Id`) is not reused for
  the request — it would blur the request/response distinction and still let a client send an
  (ignored, but confusing) `id`.

## Edge cases & risk analysis

- **Breaking change**: yes, but internal-only — `ISessionRegistrationService.RegisterAttendeeAsync`'s
  signature changes. Its only caller is `SessionsController.Register` (verified via grep — no other
  production callers), so this doesn't ripple further.
- **Swagger example collision**: `AttendeeExample`/`AttendeeRegistrationRequestExample` are two
  distinct `IExamplesProvider<T>` implementations for two distinct types, referenced from two
  distinct controllers — no conflict expected, but worth a visual check of the generated
  `/swagger/v1/swagger.json` for both operations after the change.
- **`[ApiController]` automatic validation**: property names on the new DTO are identical to
  `Attendee`'s (`FirstName`/`LastName`/`Email`), so ModelState key names in any 400 response are
  unchanged.
- No new dependencies.

## Verification & testing plan

1. `dotnet build` — confirm no compile errors after the signature change propagates.
2. `dotnet test BrownEvents.Tests --filter "FullyQualifiedName~SessionRegistrationServiceTests"` —
   all green, including the strengthened row-count assertion and the new `SessionFull` ordering test.
3. `dotnet test BrownEvents.Tests` (full suite) — confirm no regressions elsewhere (expect the same
   43 pre-existing tests plus the new one, all green).
4. Manually re-verify the fix closes the hole: the DTO structurally has no `SessionRegistrations`-
   equivalent property, so this is a compile-time proof rather than a runtime one — confirmed by
   the DTO's own definition, not a re-run of the deleted scratch repro.
5. `dotnet format BrownEvents.sln --verify-no-changes` if available in this environment (per
   CLAUDE.md, likely not — rely on manual using-directive-order review of the 2 new files and 4
   modified files instead).

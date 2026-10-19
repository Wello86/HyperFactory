# Cross-cutting notes

Gotchas and deviations from typical conventions, called out separately from [architecture.md](architecture.md) since they cut across layers/pages rather than belonging to one.

## Backend

- **`CancellationToken` propagation**: every controller action accepts a `CancellationToken` (model-bound from `HttpContext.RequestAborted`) and forwards it through the corresponding service method to each EF Core async call, so a client disconnect or request timeout cancels the in-flight DB work instead of running it to completion regardless. Service methods declare the token as optional (`= default`) so it doesn't force call-site changes where a token isn't available (e.g. `DataSeeder.cs`, which is startup-only and out of this path).
- **Lazy loading** is enabled via `UseLazyLoadingProxies()`, so every navigation property is `virtual` — accessing e.g. `session.Speaker` outside of an explicit `Include` triggers an extra synchronous DB query.
- **CORS** policy `AllowAll` (`Program.cs:41`) permits any origin/method/header — fine for local dev, not production-hardened.
- **No migrations**: schema changes require updating `Models/`/`AppDbContext.OnModelCreating` and relying on `EnsureCreated()`, which does *not* apply incremental changes to an existing database — it only creates the schema if no tables exist yet.
- **`EnableSensitiveDataLogging()`** is on for the `AppDbContext` (`Program.cs:26`) — EF Core logs parameter values (including any PII) in query/exception logs, which is a dev-only convenience and should not ship to production.
- **Transactions are provider-dependent**: `RegistrationService.RegisterAttendeeAsync` and `DataSeeder.SeedAsync` wrap their multi-`SaveChangesAsync()` calls in `_context.Database.BeginTransactionAsync()`/`CommitAsync()`, guarded by `Database.IsRelational()` — a real transaction against Npgsql, but a silent no-op against the EF Core InMemory provider the test suite uses, so no test exercises the rollback/atomicity path.
- **No authentication or authorization anywhere** — there's no `[Authorize]` attribute, no identity/auth middleware, and no JWT/cookie setup in `Program.cs` or any controller; every endpoint is fully open.

## Frontend

- **No global state.** Every page re-fetches its own data on mount; navigating away and back always re-requests, there's no cache/context/store shared across pages.
- **`console.log` debugging left in production code**: `api.js:4` (every request), and `RegistrationPage.jsx:29`. (Removed from `ConferenceDetailPage.jsx` as part of its hooks/components refactor.)
- **No component tests, no PropTypes/TypeScript** — props are consumed optimistically; a malformed or missing field (e.g. a session with no `room`) is handled ad hoc per component (`session.room && ...` guards) rather than through a shared contract.
- **Styling** is plain CSS classes from `index.css` (cards, badges, modals, forms, navbar) with occasional inline `style={{...}}` overrides directly in JSX for one-off layout tweaks — no CSS modules, no styled-components, no Tailwind.
- **`api.js`'s hardcoded `BASE_URL`** is the same gotcha flagged in the root `CLAUDE.md` — relevant any time ports or deployment targets change, since the Vite proxy silently does nothing today.

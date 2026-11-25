# Development progress

## Phase 2: Foundation (delivered 2026-11-15)
Verified locally on 2026-11-19: build succeeds, 55 domain tests and 13 integration tests pass, the migration exists, the Angular app
builds and its 10 tests pass, and both Docker images build.

Still to confirm by hand (PostgreSQL port 5432 is blocked by Windows on the developer machine, so use `POSTGRES_PORT=5433`)
- [ ] `docker compose up -d --build` starts postgres, api, web; http://localhost:8080 shows the directory
- [ ] `dotnet run --project backend/src/OneGov.Api` starts with the user-secrets connection string (port 5433)

## Phase 3, slice A: Identity (delivered 2026-11-20)
Written without a .NET SDK, so **not yet compiled**. The TOTP test helper was checked against the RFC 6238 test vector in Python.

New: domain roles, permissions and refresh-token rotation; `IAuthService` with an ASP.NET Core Identity implementation; JWT bearer
authentication; permission policies; endpoints `/api/v1/auth/{register,login,refresh,logout,mfa/enroll,mfa/confirm}`, `/api/v1/me`,
`/api/v1/audit/verify`; seeded roles and development accounts; 15 domain tests and about 18 integration tests.

Verification checklist
- [ ] `dotnet restore OneGov.slnx` (if a package version is not found, use the version restore suggests)
- [ ] `dotnet build OneGov.slnx` succeeds (also try `-p:TreatWarningsAsErrors=true`, which CI uses)
- [ ] `dotnet ef migrations add IdentityAndSessions ...` creates a migration; open it and check it creates `users`, `roles`, `user_roles`, `refresh_tokens` and does not drop anything
- [ ] `dotnet test backend/tests/OneGov.Domain.Tests` passes
- [ ] `dotnet test backend/tests/OneGov.IntegrationTests` passes (Docker running)
- [ ] API starts; sign in as `officer@example.test`; `GET /api/v1/me` works; `GET /api/v1/audit/verify` is refused until MFA is enrolled

Report any failure with the exact error text.

Slice B delivered and verified locally (81 domain and 39 integration tests): append-only audit trigger, password re-entry for MFA
enrolment, agency data isolation.

Angular sign-in slice (written, not yet run): sign-in with MFA code step, registration, account page with MFA setup and recovery codes,
auth guard, token interceptor with single refresh-and-retry, translations (en, am, om; am and om are machine drafts).
Verified locally: 27 frontend tests; sign-in screens tried in the browser.

National ID (Fayda) sign-in: backend verified (51 integration tests). Browser side written, not yet run: a "Sign in with Fayda"
button on the sign-in page (shown only when the API reports it is available), a development-only mock identity page, and a callback
page that checks the returned state matches the one it sent. To try it: run the API in Development (the mock is on there), then
open http://localhost:4200/sign-in.
## Phase 4a: applications API (written, not yet compiled)
`/api/v1/applications`: start a draft, save form data, submit, track, and staff transitions on the workflow engine. Reference numbers
(for example OG-TAXFILING-2026-000123) come from a database sequence on first submit. Rules beyond role permissions: only the applicant can submit, provide
documents or withdraw; staff cannot work on their own application; separate roles review and decide. Optimistic concurrency stops two
officers acting at once. Every action is audited. Needs a new migration (the reference sequence): `dotnet ef migrations add ApplicationReferences ...`.
Browser screens for applications (written, not yet run): apply from a service page, My applications list with state filter and paging, application page with a temporary free-text form, submit/withdraw/provide documents, staff actions with notes, and the history. Dates show in the Ethiopian and Gregorian calendars.
## Phase 4b: in-app notifications (written, not yet compiled)
`/api/v1/notifications` (list, unread count, mark read, mark all read). A notification is saved in the same database transaction as the
workflow step it describes, so it can neither be lost nor describe something that did not happen. The applicant is notified when staff act
and gets a confirmation when they submit. Stores facts (step, new state, note), so the web app shows the text in the reader's language.
Needs a new migration (the notifications table): `dotnet ef migrations add Notifications ...`.
Not built yet: email/SMS delivery through an outbox with retries, notifying staff of new submissions.

Not built yet: document upload, form schemas per service (the free-text form is a stand-in), a dedicated staff queue view.

Still to do: the real Fayda adapter (needs onboarding with the provider), QR code for authenticator setup.

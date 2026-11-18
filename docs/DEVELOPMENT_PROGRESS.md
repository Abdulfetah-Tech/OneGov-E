# Development progress

## Phase 2: Foundation (delivered 2026-11-15)
Written without access to a .NET SDK, Docker or npm. **Nothing has been compiled or run yet.** Logic that could be checked
independently was: the Ethiopian calendar algorithm (known dates + 73,000-day round trip) and the i18n key check.

Verification checklist (tick each when done)
- [ ] `dotnet build OneGov.slnx` succeeds
- [ ] `dotnet ef migrations add InitialFoundation ...` creates the migration
- [ ] `dotnet test backend/tests/OneGov.Domain.Tests` passes
- [ ] `dotnet test backend/tests/OneGov.IntegrationTests` passes (Docker running)
- [ ] API starts; `/health/ready` healthy; `/api/v1/services` returns the seeded services
- [ ] In `frontend/onegov-web`: `npm ci`, `npm run i18n:check`, `npm run build`, `npm test` pass
- [ ] `docker compose up -d --build` starts postgres, api, web; http://localhost:8080 shows the directory

Report any failure with the exact error text.

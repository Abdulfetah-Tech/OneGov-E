# OneGov

A unified e-government platform prototype for Ethiopia: one place to find government services, understand their
requirements and follow applications, in English, Amharic and Afaan Oromo.

> **Status: foundation (Phase 2).** Everything uses sample data. Nothing is connected to a real government system,
> real tax rules or the real national ID. See `docs/ROADMAP.md` for what exists and what is planned.

**Stack:** .NET 10 (ASP.NET Core, EF Core), PostgreSQL 16, Angular, Docker. Modular monolith with extractable module boundaries
(ADR 0001).

## What works today

- Public service directory API (search, category filter, pagination, three languages with English fallback)
- Domain core, fully unit-tested: configurable **workflow engine**, **hash-chained audit log**, **Ethiopian calendar** conversion
- PostgreSQL persistence with soft delete, audit timestamps and optimistic concurrency
- Health checks, structured logging, problem-details errors, rate limiting, security headers, OpenAPI
- Angular app shell: home, service directory, service detail, language switcher, accessible layout

Not built yet: login/MFA/roles, applications and documents, notifications, officer/admin areas, AI assistant (see roadmap).

## Quick start (Windows PowerShell, from the repository root)

Prerequisites: .NET 10 SDK, Docker Desktop, Node 24 + npm, Git.

```powershell
# 1. Settings
copy .env.example .env                    # then edit POSTGRES_PASSWORD

# 2. Database
docker compose up -d postgres

# 3. Connection string for local runs (stored outside the repo)
dotnet user-secrets set "ConnectionStrings:Default" "Host=localhost;Port=5432;Database=onegov;Username=onegov;Password=YOUR_POSTGRES_PASSWORD" --project backend/src/OneGov.Api

# 4. First migration (the repository does not ship one; this creates it from the model)
dotnet tool restore
dotnet ef migrations add InitialFoundation --project backend/src/OneGov.Infrastructure --startup-project backend/src/OneGov.Api --output-dir Persistence/Migrations

# 5. Build and test
dotnet build OneGov.slnx
dotnet test backend/tests/OneGov.Domain.Tests               # fast, no Docker
dotnet test backend/tests/OneGov.IntegrationTests           # needs Docker running

# 6. Run the API (applies migrations and seeds sample data in Development)
dotnet run --project backend/src/OneGov.Api
```
Then open http://localhost:5080/scalar (API reference), http://localhost:5080/health/ready and
http://localhost:5080/api/v1/services.

Frontend (second terminal): `cd frontend/onegov-web`, then `npm ci` and `npm start` (http://localhost:4200). See `frontend/onegov-web/README.md`.

Whole stack in containers (after step 4 has created the migration): `docker compose up -d --build`, then http://localhost:8080.

Commit the generated `Persistence/Migrations` folder: migrations are source code.

## Repository layout

```
backend/src/OneGov.Domain          pure business rules (no I/O): workflow, audit chain, calendar, registry, applications
backend/src/OneGov.Application     use cases and ports (interfaces), validation
backend/src/OneGov.Infrastructure  EF Core, PostgreSQL, adapters
backend/src/OneGov.Api             HTTP API, composition root
backend/tests/                     unit tests and Testcontainers integration tests
frontend/onegov-web                Angular app (standalone components, signals, runtime i18n)
docs/                              architecture, ADRs, roadmap, security baseline
```

## Engineering rules

Tests with every feature; CI builds with warnings as errors; no secrets in Git; sample data only; every external
integration is an adapter with a clearly labelled mock; no claim of compliance, performance or integration without evidence.

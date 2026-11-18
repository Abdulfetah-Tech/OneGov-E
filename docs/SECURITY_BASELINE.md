# Security baseline

Status of each control. "Planned" items are not implemented yet and must not be assumed.

| Control | Status |
|---|---|
| Parameterised queries only; search wildcards escaped | done |
| Input validation on public endpoints | done (directory) |
| Problem responses never leak internals; trace id included | done |
| Security headers on all responses (nosniff, frame deny, referrer, permissions policy; CSP outside Development) | done |
| CORS allow-list from configuration | done |
| Rate limiting per client IP (needs forwarded headers behind a proxy) | done |
| Audit entries append-only in the app and tamper-evident (hash chain, verified) | done |
| No secrets in Git; connection string via user-secrets / environment | done |
| Authentication, MFA, session/refresh-token handling | planned (Phase 3) |
| Authorization with permissions and agency isolation in the data layer (PostgreSQL row-level security evaluated) | planned (Phase 3) |
| Database trigger forbidding UPDATE/DELETE on `audit_entries` | planned (Phase 3) |
| Encrypted document storage, virus-scan adapter, signed short-lived download links | planned (Phase 4) |
| Dependency and container vulnerability scanning in CI | planned (Phase 8) |
| TLS termination, HSTS, secrets manager, encrypted backups, restore test | planned (Phase 9) |

No claim of legal or regulatory compliance is made. A legal assessment is required (see ADR 0006).

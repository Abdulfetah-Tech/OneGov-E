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
| Local accounts: PBKDF2 hashing, length-based password policy (min 10), lockout after 5 failures for 15 min | implemented in slice A, awaiting local verification |
| TOTP MFA with single-use recovery codes; wrong codes count toward lockout | implemented in slice A, awaiting local verification |
| Short-lived (10 min) signed access tokens; rotating refresh tokens stored as hashes, family revoked on reuse, 30-day absolute limit | implemented in slice A, awaiting local verification |
| Staff-only permissions require a token issued after MFA; separation of duties between review, verify and decide | implemented in slice A, awaiting local verification |
| MFA enrolment re-checks the password; wrong attempts count toward lockout | implemented in slice B (awaiting local verification) |
| Document uploads: type by signature, size and count limits, sanitised names, random storage keys, applicant-only changes while open, downloads audited and always attachments. No malware scanning or application-level encryption yet | implemented in slice 4c (awaiting local verification); scanning planned |
| Stricter rate limit on sign-in, registration and token endpoints; auth responses `no-store` | implemented in slice A, awaiting local verification |
| Breached-password check, email verification, password reset, TOTP replay protection | planned (Phase 3 slice B / Phase 4) |
| Permission policies on endpoints | implemented in slice A (`audit:read`); other permissions are enforced as their endpoints arrive |
| Agency isolation in the data layer: an EF query filter shows citizens their own applications and staff (with MFA) their agency's; anonymous, administrator and unknown callers see none. Reads only. Writes are checked in the application service when it is built (Phase 4). PostgreSQL row-level security is a possible second layer | implemented in slice B (awaiting local verification) |
| Database trigger forbidding UPDATE/DELETE/TRUNCATE on `audit_entries`. The table owner can still disable it, so production must run the API as a role that does not own the table; the hash chain detects tampering either way | implemented in slice B (awaiting local verification) |
| Encrypted document storage, virus-scan adapter, signed short-lived download links | planned (Phase 4) |
| Dependency and container vulnerability scanning in CI | planned (Phase 8) |
| TLS termination, HSTS, secrets manager, encrypted backups, restore test | planned (Phase 9) |

No claim of legal or regulatory compliance is made. A legal assessment is required (see ADR 0006).

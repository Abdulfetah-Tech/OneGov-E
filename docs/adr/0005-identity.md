# ADR 0005: Identity

Status: accepted for local accounts (Phase 3 slice A); Fayda adapter still proposed

Context: Ethiopia's national digital ID (Fayda) is reported to publish OpenID Connect authentication (eSignet), a partner portal and a
developer sandbox. OneGov should consume it instead of inventing a parallel national identity.

Proposal: ASP.NET Core Identity for local accounts (development, staff, fallback; default PBKDF2 hasher, a custom Argon2id hasher to be evaluated), TOTP MFA, short-lived
JWT access tokens and rotating refresh tokens with reuse detection. A `IIdentityVerifier` / OIDC adapter for Fayda, mock first,
sandbox when onboarding is granted. Whether "ID renewal" matches a real process must be verified with the responsible agency.

Open: partner onboarding, staff authentication policy, session lifetime.

## Decisions made in slice A
- ASP.NET Core Identity (`UserManager`) for accounts, password hashing, lockout and TOTP. No hand-written cryptography beyond SHA-256 hashing of refresh-token secrets.
- Password policy follows length over composition (min 10, 5 distinct characters). A breached-password check is still to do.
- Access tokens are HS256 JWTs valid 10 minutes. They carry `sub`, `email`, `role`, `amr` (`pwd`, plus `otp` after MFA) and `agency`. Permissions are derived from roles on every request, not stored in the token.
- Refresh tokens are random 256-bit secrets returned in the response body; only the SHA-256 hash is stored. Each use rotates the token. Reuse of a rotated or revoked token revokes the whole family. Concurrent use of one token is resolved with PostgreSQL `xmin` optimistic concurrency. Families end after 30 days regardless of activity.
- Staff-only permissions are refused unless the token's `amr` contains `otp`. The flag is carried through refresh.
- Roles: citizen, officer, approver, agency_admin, platform_admin. Reviewing and deciding are held by different roles, and administrators hold no case permissions.

## Known limitations of slice A
- Email addresses are not verified and there is no password reset yet (needs the Phase 4 notification outbox). Registration reveals whether an email is taken; the stricter rate limit is the only mitigation until then.
- Access tokens stay valid for up to 10 minutes after logout, a role change or an account being disabled.
- A TOTP code can be reused within its time window (Identity does not track used codes).
- A client that loses the response to a refresh and retries with the old token is signed out (the price of reuse detection).
- Refresh tokens live in the browser. Whether to move them to an HttpOnly cookie is decided when the Angular login screens are built.
- HS256 with a shared secret is fine for one API. Move to asymmetric keys if other services must validate tokens.

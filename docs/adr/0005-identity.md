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
- Browser storage (decided in the Angular sign-in slice): the access token is kept in memory only; the refresh token is kept in sessionStorage, so a reload keeps the session and closing the tab ends it. Script injected into the page could read it, so the app must load no third-party scripts and should get a strict Content-Security-Policy. Moving the refresh token to an HttpOnly cookie (with CSRF protection) is to be done before production.
- National ID (Fayda) sign-in is built behind a port, `INationalIdProvider`, with a disabled provider (default) and a mock for development and tests. The mock refuses to run outside Development and Testing. The real adapter needs onboarding with the identity provider and will be written against their documentation, not from memory. First sign-in creates a citizen account linked by the provider's pseudonymous subject (ASP.NET Identity external login `national-id`); the provider's email is never trusted, so accounts are never merged by email. The state value is a sealed 10-minute token (ASP.NET data protection); the web app also keeps it in sessionStorage and must check it matches on return. A national-ID account has no password, so it cannot use password sign-in, and staff roles still require an authenticator code.
- The sign-in screen does not show a QR code for authenticator apps yet. The setup key is shown for typing by hand.
- HS256 with a shared secret is fine for one API. Move to asymmetric keys if other services must validate tokens.

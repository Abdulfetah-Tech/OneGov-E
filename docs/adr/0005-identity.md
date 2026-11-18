# ADR 0005: Identity

Status: proposed (Phase 3)

Context: Ethiopia's national digital ID (Fayda) is reported to publish OpenID Connect authentication (eSignet), a partner portal and a
developer sandbox. OneGov should consume it instead of inventing a parallel national identity.

Proposal: ASP.NET Core Identity for local accounts (development, staff, fallback; default PBKDF2 hasher, a custom Argon2id hasher to be evaluated), TOTP MFA, short-lived
JWT access tokens and rotating refresh tokens with reuse detection. A `IIdentityVerifier` / OIDC adapter for Fayda, mock first,
sandbox when onboarding is granted. Whether "ID renewal" matches a real process must be verified with the responsible agency.

Open: partner onboarding, staff authentication policy, session lifetime.

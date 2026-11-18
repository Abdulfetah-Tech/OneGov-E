# Roadmap

Estimates: one full-time developer with an AI coding agent. Not included: legal review, agency onboarding, translator review.

| # | Phase | Status | Contents |
|---|---|---|---|
| 1 | Discovery | done | Audit, requirements, architecture, risks (separate documents) |
| 2 | Foundation | **delivered, awaiting local verification** | Solution, domain core, registry API, audit chain, Docker, CI, Angular shell, i18n, calendar |
| 3 | Identity and security (2 wk) | next | Registration, login, TOTP MFA, refresh-token rotation, roles and permissions, agency scoping in the data layer, Fayda OIDC adapter (mock first) |
| 4 | Workflow and documents (2 wk) | planned | Persisted applications on the workflow engine, reference numbers, document upload/storage, notifications, outbox |
| 5 | Services (2 wk) | planned | Tax filing and ID renewal citizen flows (sample rules), dashboard, tracking |
| 6 | Agency and admin (2.5 wk) | planned | Officer queues, verification, admin screens, integration adapters and mock agencies |
| 7 | Access and AI (2 wk) | planned | PWA/offline drafts, facilitator role, AI assistant with strict guardrails |
| 8 | Quality (2 wk) | planned | Full E2E, security review, accessibility audit, load tests |
| 9 | Deployment (1.5 wk) | planned | Production images, manifests, backups, runbook (after the hosting decision, ADR 0006) |

## Definition of done for the MVP
Citizens register and log in with MFA; both workflows run end to end with synthetic data; officers process applications;
agency isolation, RBAC and audit are tested; uploads are secure; E2E flows pass; `KNOWN_LIMITATIONS` lists every mock.

## Blocked on decisions outside engineering
Hosting location and data residency (ADR 0006); Fayda partner onboarding; official tax and ID rules; native-speaker review of translations.

# ADR 0006: Hosting and data residency

Status: open, blocks production deployment

Context: Secondary sources report that Ethiopia's Personal Data Protection Proclamation No. 1321/2024 requires locally collected
personal data to be stored on servers in Ethiopia and restricts cross-border transfer. The original brief assumes AWS and
may call external AI providers. These sources are summaries, not the official text.

Decision for now: keep the whole stack self-hostable (containers + PostgreSQL), use cloud-neutral manifests, and send no citizen data
to external services (AI assistant defaults to a local model). No production hosting choice is made.

Needed before production: legal assessment of localisation, retention, consent and cross-border rules; a hosting provider that satisfies it.

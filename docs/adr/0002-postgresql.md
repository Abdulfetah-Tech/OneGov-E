# ADR 0002: PostgreSQL as the primary store

Status: accepted (the original brief named MongoDB; changed deliberately)

Context: Application state changes, history and audit entries must commit together. Agency isolation and referential integrity matter.
Form payloads vary by service.

Decision: PostgreSQL. Variable form data is stored as `jsonb`. Optimistic concurrency uses `xmin`. Row-level security is the planned
way to enforce agency isolation below the application code (Phase 3).

Consequences: Strong transactions and constraints. JSON flexibility without a second database. Team must be comfortable with SQL migrations.

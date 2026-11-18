# ADR 0004: Configuration-driven workflow engine

Status: accepted

Decision: Services are versioned declarative state machines (states, named transitions, required permission, optional required note).
The engine is a pure function over (definition, current state, transition, permissions, note). Applications record which workflow
version they started with, so changing a definition never changes in-flight applications.

Consequences: Tax filing and ID renewal are configurations, not code paths; adding a service means adding a definition. Built-in
definitions are SAMPLES and encode no official rule. Definitions will move to the database (editable by administrators) in Phase 4.

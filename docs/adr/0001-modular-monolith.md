# ADR 0001: Modular monolith first

Status: accepted

Context: The brief describes microservices on Kubernetes. A national-scale design is the destination, but an MVP split into ten
services multiplies deployment, data-consistency and debugging cost before any citizen value exists.

Decision: One ASP.NET Core application with strict module boundaries (own tables, interface-only access, events through an
outbox). Only mock agency systems run as separate processes, to prove the integration layer.

Consequences: Simple deployment and transactions now. A module can be extracted later because its boundary is already enforced.
Revisit when a module needs independent scaling or a separate team.

# ADR 0003: Hash-chained audit log

Status: accepted

Decision: Each audit entry stores the hash of its predecessor; the hash covers a canonical, length-prefixed encoding of all fields
(timestamps as Unix milliseconds so database round-trips cannot change them). Appends are serialised with a PostgreSQL advisory lock.
The application refuses to update or delete entries; verification walks the chain in batches.

Limits (honest): a database administrator who rewrites every later hash can forge the chain. Mitigations planned: database trigger
forbidding UPDATE/DELETE, periodic anchoring of the latest hash to an external write-once location, restricted database roles.
"Tamper-evident" is the claim; "tamper-proof" is not.

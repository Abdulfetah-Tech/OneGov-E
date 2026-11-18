# ADR 0007: Runtime translations and the Ethiopian calendar

Status: accepted

Decision: Runtime JSON dictionaries (one file per language, loaded on demand with English fallback) so users download only what they need
and language can change without reloading. Every non-English file carries a review status; `npm run i18n:check` fails CI on missing keys or
placeholder mismatches. Content in the database is localised through `LocalizedText` with English fallback.
Dates: the Ethiopian calendar is implemented identically in C# and TypeScript with shared test vectors; conversions take plain
year/month/day numbers to avoid time-zone shifts.

Consequences: Amharic and Afaan Oromo strings in this repository are machine drafts and must be reviewed by native speakers before any official use.

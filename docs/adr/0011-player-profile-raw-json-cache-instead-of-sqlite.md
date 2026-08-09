# ADR 0011: Player profile uses a JSON RAW cache instead of SQLite

## Status

Accepted

## Context

`docs/MASTER_PLAN.md` specifies SQLite for persistence. Fase 2 (Player
Profile) needs to cache OpenDota's public API responses (player summary,
match history, hero stats) per `dota-data`'s requirement: cache RAW
responses by endpoint, never call the live API from anything downstream,
attach fetch timestamp and source to every stored record.

At the point this phase started, the solution had zero database
dependencies, zero ORM, no `Microsoft.Data.Sqlite` reference, and no DI
container to wire one through cleanly.

## Decision

Cache raw OpenDota responses as JSON files on disk
(`%LOCALAPPDATA%\DotaAiCoach\cache\opendota\`), one file per endpoint, each
wrapped in an envelope (`endpoint`, `fetchedAtUtc`, `statusCode`,
`sourceName`, `schemaVersion`, `body`) with `body` stored verbatim. Writes
are atomic (temp file + `File.Move(overwrite: true)`). No SQLite, no ORM,
no new `PackageReference` — `System.Text.Json` is already in the net8.0
shared framework.

## Consequences

- What `dota-data` actually requires — "cache RAW responses by endpoint,
  attach provenance" — is document-shaped, not relational. A JSON blob is
  a more direct fit than forcing OpenDota's nested shapes (`counts`,
  `heroes[]`) into tables prematurely.
- Offline-first (the app must work without network mid-match) is
  satisfied identically by a stale JSON file as by a stale DB row.
- SQLite/DuckDB genuinely becomes necessary in **Fase 3** (bulk historical
  match ingestion for ML training, per `dota-data`'s RAW → BRONZE →
  NORMALIZED → FEATURES → DATASET pipeline). When that phase starts, this
  JSON cache becomes the pipeline's RAW layer — not something to discard —
  and BRONZE onward is where a real relational/columnar store (DuckDB +
  Parquet, per `dota-data`) belongs.
- This is a deliberate, documented deviation from `docs/MASTER_PLAN.md`'s
  "SQLite" line and `docs/ROADMAP.md` has been updated to say so rather
  than silently contradicting it.
- Revisit trigger: the moment Fase 3 bulk ingestion work begins, re-read
  this ADR before reaching for SQLite by default — the RAW cache built
  here should be reused, not replaced.

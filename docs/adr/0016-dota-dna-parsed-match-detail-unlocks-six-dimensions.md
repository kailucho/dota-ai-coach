# ADR 0016: Parsed match detail unlocks six more Dota DNA dimensions

## Status

Accepted. Partially supersedes `docs/adr/0014`'s per-dimension table.

## Context

ADR-0014 shipped 4 of 11 Dota DNA dimensions, deferring the other 7 because
OpenDota's unparsed match-summary endpoints (`/players/{id}/matches`,
`/players/{id}/recentMatches`) don't carry purchase timing, ward counts,
buyback usage, or lane efficiency. OpenDota exposes that data only after a
match is explicitly parsed: `POST /request/{match_id}` triggers an async
job; polling `GET /request/{jobId}` returns `null` once complete; a
subsequent `GET /matches/{match_id}` then returns a much richer shape.
Verified directly against the live API and against a real user account's
match, captured at
`tests/DotaAiCoach.OpenDota.Tests/Fixtures/matches_8936354085_parsed.json`.

Parsing costs meaningfully more of OpenDota's rate-limit budget than a
normal read (`dota-data`'s ~10x figure), so it cannot be triggered
automatically on every profile refresh.

## Decision

**Six dimensions promoted to `Computed`**, each self-relative like the
original four and gated by the same `MinSamples = 5` threshold:

| Dimension | Field(s) used |
|---|---|
| Laning | `lane_efficiency_pct` (OpenDota's own metric), averaged |
| Objectives | `tower_kills`, `roshan_kills`/`roshans_killed`, self-relative |
| Vision | `obs_placed` + `sen_placed`, self-relative |
| Itemization | Purchases within the first 10 minutes from `purchase_log`, self-relative — a role-agnostic proxy, deliberately not "time to a specific key item" since that's hero/role dependent |
| BuybackDiscipline | Buybacks per match from `buyback_log`, inverted (fewer = more disciplined) |
| Tempo | Share of final gold already reached by minute 10, from `gold_t` — upgraded from ADR-0014's `NotYetImplemented`; a real signal, unlike the duration-distribution proxy that was judged too thin |

**Positioning stays deferred**, reclassified from `RequiresReplay` to
`NotYetImplemented` with an updated, honest reason: parsed data (`lane_pos`/
`position_est`) exists but is heatmap-shaped grid-bucket counts, not a
real trajectory signal — computing a "Positioning" score from it would be
misleading rather than merely thin.

**Parse-triggering policy**: never automatic. A new explicit "Deep DNA
Refresh" user action (`IProfileSession.RequestDeepDnaRefreshAsync`) parses
up to 20 of the player's most recent ranked (non-Turbo) matches, reusing
the existing `RateLimiter`/`CachingOpenDotaClient` infrastructure
unmodified — POST/poll/GET all flow through the same rate-limited
`IOpenDotaClient`. `ParsedMatchRequester` bounds polling to a fixed max
attempt count (never waits forever for a job that never completes) and
skips matches whose parsed detail is already cached.

**Cache freshness**: `RawResponseCache.CachedEntry` gained an explicit
`IsParsed` flag, set by the writer (never inferred by sniffing the
response JSON shape, since shape-sniffing heuristics drift as OpenDota's
schema evolves). Parsed match detail for a finished match never changes,
so `HasParsedMatchCachedAsync` treats a cached parsed entry as
permanently fresh — no TTL expiry.

**New types**: `DotaAiCoach.PlayerProfile.Model.ParsedMatchDetail` (the
domain-facing record `DotaDnaBuilder` consumes, decoupled from OpenDota's
wire shape — same pattern as `PlayerMatchSummary`) and
`DotaAiCoach.OpenDota.Wire.OpenDotaParsedMatchDto`/`OpenDotaParsedPlayerDto`
(narrowly modeling only the fields the six dimensions need, not the full
captured shape — `objectives`/`teamfights`/`chat` etc. are out of scope).

## Consequences

- `DotaDnaBuilder.Build` gained an optional `parsedMatches` parameter
  (default `null`/empty) — a normal `FetchAsync` never populates it, so
  the six new dimensions report `InsufficientData` (not a fabricated
  score) until a Deep DNA Refresh has run at least once. This was
  verified live: a normal refresh against the user's real account showed
  the original four dimensions computed and the rest `InsufficientData`;
  after a Deep DNA Refresh (20 matches parsed and cached in
  `%LOCALAPPDATA%\DotaAiCoach\cache\opendota\`), 10 of 11 dimensions
  showed real computed scores.
- `IOpenDotaClient` gained `PostAsync` (only ever used for
  `/request/{match_id}`); `CachingOpenDotaClient.PostAsync` never caches
  or serves offline — a parse trigger only makes sense as a live call.
- FairPlay: `ParsedMatchDetail` carries no `[FairPlaySource]` attribute of
  its own since it's not a top-level snapshot root — it's the same
  OpenDota `PublicPreflight`/`ExternalApi` data as `PlayerProfileSnapshot`,
  just richer, and flows only into `DotaDnaProfile` (never
  `LiveFeatureSet`). `ProfileFairPlayTests` gained a regression test
  confirming `ParsedMatchDetail` is unreachable from `LiveFeatureSet`.

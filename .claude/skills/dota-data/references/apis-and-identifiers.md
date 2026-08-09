# APIs and Identifiers

Neither OpenDota nor STRATZ publish a fully authoritative, versioned
spec the way a typical commercial API does. What's below is grounded in
their commonly-documented/observed behavior as of this skill's writing,
with explicit confidence flags. Re-verify anything rate-limit-related or
STRATZ-specific against current docs before depending on exact numbers.

## OpenDota API

- **Base URL:** `https://api.opendota.com/api`
- **Auth:** API key optional, passed as `?api_key=...`. An unauthenticated
  caller gets lower rate limits; a free API key raises them; a paid tier
  raises them further. **Verify current numeric limits before hardcoding
  a rate limiter** — these have changed over time and shouldn't be
  trusted from memory.
- **Key endpoints:**
  - `GET /matches/{match_id}` — full match detail (unparsed fields
    always present; parsed fields present only if the match has been
    parsed).
  - `GET /players/{account_id}` — player profile summary.
  - `GET /players/{account_id}/matches` — a player's match history.
  - `POST /request/{match_id}` — triggers replay parsing for a match not
    yet parsed; costs substantially more rate-limit budget than a normal
    read (commonly cited as ~10x) — don't call this casually in a bulk
    ingestion loop without accounting for that cost.
- **Match object shape (commonly observed, not a guaranteed contract):**
  `players[]` (per-player hero_id, account_id, K/D/A, items, etc.),
  `picks_bans[]`, `game_mode`, `lobby_type`, `patch` (see below),
  `radiant_win`, `duration`, `start_time` (Unix timestamp). Parsed
  matches add timeline/position data not present on unparsed matches.

## STRATZ API

- **Type:** GraphQL, endpoint `https://api.stratz.com/graphql`.
- **Auth:** Bearer token via `Authorization: Bearer <token>` header,
  requires a STRATZ account API key.
- **Rate limits:** not independently confirmed at time of writing — check
  STRATZ's current docs/knowledge base directly rather than assuming a
  number.
- **When to prefer STRATZ over OpenDota:** commonly reputed to offer
  richer parsed/historical data and more flexible querying via GraphQL,
  but this project shouldn't build STRATZ-specific logic on unverified
  assumptions — confirm the specific field/capability needed actually
  exists in current STRATZ docs before designing around it.

## Steam IDs

- OpenDota's `account_id` field is a **Steam32** ID (the 32-bit
  "friend code" style ID), not Steam64.
- Conversion: `steam64 = steam32 + 76561197960265728` (standard Steam ID
  offset, not OpenDota-specific).
- Pick one canonical form to store per project convention and label it
  explicitly in column/field names (e.g. `account_id_steam32`, not just
  `account_id`, if there's any chance of ambiguity downstream) — a mixed
  or unlabeled ID column is a recurring source of silent join bugs when
  joining against a different data source that uses the other form.

## Match IDs

Globally unique per match, consistent across OpenDota, STRATZ, and the
Steam/Dota client itself — this is the natural join key across data
sources and across pipeline stages (RAW through DATASET) for a given
match. Every pipeline stage should carry `match_id` through untouched;
never regenerate or reassign it.

## Patch IDs

A match's patch is either:
- present directly as a field on the match object (an index into a
  patch list, e.g. via `odota/dotaconstants`'s `patch.json`), or
- absent, requiring derivation from `start_time` against a patch-date
  lookup table.

Either way, **every stored match-level record in NORMALIZED and later
stages must carry an explicit patch value** — don't rely on being able to
re-derive it later from `start_time`, because the patch-date lookup table
itself can need updating/correcting over time, and a record without a
stored patch can silently get the *wrong* derived patch after such an
update, corrupting historical results without any visible error. See
`data-quality-rules.md`, rule 3.

## Rank tiers

Commonly represented as a two-digit `rank_tier` integer: tens digit is
the medal (Herald through Immortal), ones digit is a star (1-5, with
Immortal typically using a separate `leaderboard_rank` for exact
position instead of stars). Match objects may also expose an aggregate
rank across all ten players (e.g. `average_rank`/`avg_rank_tier`).
**Confirm the exact current digit encoding against `dotaconstants` or a
live match object before hardcoding a Herald-through-Immortal mapping** —
don't trust a remembered mapping to still be exactly right.

## Game mode / lobby type (Turbo vs Ranked, etc.)

Two separate fields jointly determine what kind of match this was:

- `game_mode` — the drafting/game-mode format (e.g. All Pick, All Draft,
  Turbo, Captain's Mode, All Random, Ability Draft — numeric codes are in
  `odota/dotaconstants`'s `game_mode.json`, verify current values there
  rather than hardcoding from memory).
- `lobby_type` — the matchmaking context (e.g. Normal/unranked, Ranked,
  Practice, Battle Cup, Spectator — codes in `lobby_type.json`).

**Turbo is identified by `game_mode`, not `lobby_type`** — a Turbo match
is not itself a "ranked lobby," so filtering on `lobby_type` alone is not
sufficient to separate Turbo from normal Ranked All Pick. Both fields
must be checked together to classify a match correctly for the
Turbo/Ranked separation rule in `data-quality-rules.md`. Verify current
numeric codes against `dotaconstants` before writing a filter — don't
hardcode codes into pipeline logic from memory without checking the
source constants file, since codes can be added/reused across patches.

## Replays

Raw `.dem` replay files are the ultimate source of truth for a match, but:
- OpenDota only exposes timeline/position/detailed event data for matches
  that have been explicitly **parsed** (`POST /request/{match_id}`);
  unparsed matches only expose summary-level data (K/D/A, items,
  duration, outcome).
- Parsing costs meaningfully more API budget than a normal read — budget
  for this explicitly in any bulk-ingestion plan rather than parsing
  every match by default. Parse selectively, based on what the FEATURES
  stage actually needs (e.g. only parse matches needed for
  timeline-dependent features).
- If a feature genuinely requires data not available even from a parsed
  OpenDota/STRATZ match, direct `.dem` parsing is the fallback — but this
  is a heavier, slower path and should be a deliberate, scoped decision,
  not a default.

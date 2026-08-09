# ADR 0014: Dota DNA ships four computed dimensions and explicitly defers seven

## Status

Accepted

## Context

`docs/MASTER_PLAN.md` lists eleven Dota DNA dimensions: Laning, Farm,
Fighting, Positioning, Objectives, Itemization, Survival, Vision, Tempo,
Late Game, and (added during this phase, since buyback behavior is
explicitly called out elsewhere in the MASTER_PLAN's player-analysis
section) Buyback Discipline. OpenDota's unparsed match summary endpoints
(`/players/{id}/matches`, `/players/{id}/recentMatches`) only expose
K/D/A, GPM/XPM, hero/tower damage, duration, lane/lane_role — not ward
placement, purchase timing, buyback usage, or positional data, all of
which require OpenDota's *parsed* match detail (`POST /request/{match_id}`
then re-fetching) or raw replay parsing.

## Decision

`DotaDnaBuilder` always emits all eleven `DnaDimensionScore` records, one
per `DotaDnaDimension`. Four are actually computed from unparsed match
summaries:

| Dimension | Basis |
|---|---|
| Farm | `gold_per_min`, self-relative to the player's own recent range |
| Fighting | `kills`+`assists`+`hero_damage`, self-relative |
| Survival | `deaths` per 10 minutes, inverted, self-relative |
| Late Game | Bayesian-shrunk win rate in matches over 40 minutes |

The other seven report a truthful `DnaAvailability`
(`RequiresParsedMatches`, `RequiresReplay`, or `NotYetImplemented` for
Tempo, whose only available proxy — match duration distribution — was
judged too thin to name as a real signal) and a `null` score, never a
fabricated number.

## Consequences

- The UI (`ProfileViewModel`/`ProfileWindow`) renders a per-dimension note
  ("Needs parsed match data", "Needs replay analysis") instead of a bar
  for deferred dimensions — verified live against the user's real OpenDota
  account (`k4ido`, account_id 86845504): 4 dimensions computed, 7
  deferred with their true reason, no invented scores.
- Computed dimensions apply the same self-relative, sample-gated
  (`MinSamples = 5`) approach as `HeroComfortScorer`'s components — no
  small-sample dimension claims a confident score.
- Extending a deferred dimension to `Computed` later (e.g. once parsed-
  match ingestion exists) only requires changing that dimension's builder
  method in `DotaDnaBuilder`; the record shape and the "always emit all
  eleven" contract don't change.

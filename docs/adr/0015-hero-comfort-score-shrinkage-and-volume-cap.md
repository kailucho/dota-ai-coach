# ADR 0015: Hero Comfort Score uses Bayesian shrinkage plus a hard volume cap

## Status

Accepted

## Context

`docs/MASTER_PLAN.md` requires a 0-100 Hero Comfort Score that must not
use raw win rate on small samples, explicitly stating: *"2 partidas / 2
victorias NO debe producir comfort 100."* It calls for Bayesian shrinkage,
empirical Bayes, or confidence intervals.

## Decision

`HeroComfortScorer` (`DotaAiCoach.PlayerProfile.Scoring`) applies two
independent defenses so no single mechanism carries the whole guarantee:

1. **Win rate shrinkage toward the player's own prior.** The player's
   overall win rate is itself shrunk toward 0.5 with pseudo-count
   `k0 = 50`: `μ = (W + k0·0.5) / (N + k0)`. That `μ` becomes the mean of a
   Beta prior with pseudo-count `k = 20` (`α = μk`, `β = (1-μ)k`), giving
   `pHat = (w + α) / (n + α + β)`. A Wilson lower confidence bound
   (`z = 1.28`) over the posterior pseudo-sample produces the final
   `AdjustedWinRate`. Worked example: 2 games / 2 wins with `μ ≈ 0.5`
   gives `pHat = 12/22 ≈ 0.545`, not 1.0.
2. **A hard cap tied to evidence volume, independent of the weighting**:
   `Score = min(weighted_score, 100 × (0.55 + 0.45 × VolumeFactor))`, where
   `VolumeFactor = min(1, log(1+n) / log(1+40))`. At `n = 2`,
   `VolumeFactor ≈ 0.30`, so the ceiling is ≈68 — structurally impossible
   to reach 100 regardless of how any individual weighted component
   behaves. This line is the single most important guarantee in the
   algorithm and is covered by a dedicated regression test
   (`Two_games_two_wins_does_not_produce_comfort_100_...`).

Components (`AdjustedWinRate`, `VolumeFactor`, `RecencyFactor`,
`PerformanceFactor`, `Consistency`, `PatchFamiliarity`,
`RoleFamiliarity`, `ComplexityPenalty`) are each `[0,1]` or `null`; `null`
components redistribute their combination weight rather than counting as
zero, per the project-wide "null means undecidable" rule.
`MatchupFamiliarity` is modeled but left `null` in this phase — OpenDota's
`/heroes` endpoint only reports matchup data at the *player* level (record
against each enemy hero across all of the player's heroes), not per
played-hero, and faking a per-hero number from that endpoint would be
wrong; a future phase can compute it once a suitable data source exists.

## Consequences

- Verified against the user's real account in production: hero comfort
  scores across 127 heroes ranged 30-65 with game counts from 55 to 524 —
  no small-sample hero produced an inflated score, and scores scaled
  sensibly with volume.
- `HeroComfortOptions` exposes every constant above as a tunable POCO
  property (same pattern as `GsiOptions`/`LiveMessageBudgetOptions`), so
  the specific pseudo-counts/half-life/weights can be retuned without
  touching the algorithm's structure.
- `Confidence` is computed and reported separately from `Score` (component
  completeness × volume) and is never folded into the score itself, so a
  low-confidence score is still visible as a number, with its confidence
  qualifying it rather than suppressing it.

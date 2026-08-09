# Patch-Aware and Bracket-Aware Analysis

## Why this is load-bearing, not a nice-to-have

Hero balance shifts by patch, and what's practically correct shifts by
bracket (higher brackets execute coordinated plans and punish mistakes
more consistently; lower brackets see different real-world win rates for
the same theoretical matchup because of execution variance). Computing
`MetaStrength`, `DraftFit`, or `LaneFit` from patch-blind or
bracket-blind data produces a number that looks precise but is answering
the wrong question — this is the same failure mode the core rule
explicitly warns about (a global counter statistic), just generalized:
*any* insufficiently-conditioned aggregate statistic has the same
problem, whether the missing condition is "this specific draft" (the
core rule) or "this patch/bracket" (this section).

## Patch-awareness requirements

- **MetaStrength must be computed from the current patch's data**, not
  an all-time aggregate. Per `dota-data`'s patch-tagging rule, every
  underlying match record used for this computation must carry an
  explicit patch field — join/filter on it directly rather than assuming
  recency in the data ordering implies patch relevance.
- **When the current patch is very recent (little data yet accumulated)**,
  don't silently fall back to blending in older-patch data as if it were
  equally relevant — reflect the resulting uncertainty explicitly (lower
  confidence on MetaStrength for a hero/matchup with only current-patch
  data, per `dota-item-recommender`'s confidence-and-bias approach) rather
  than quietly diluting the current-patch signal with stale data to get a
  bigger sample.
- **DraftFit's mechanism-based components** (composition-dimension
  analysis) are less patch-sensitive in principle (a stun is a stun) but
  can still be patch-sensitive in practice if a hero's kit changed this
  patch (a facet/talent rework, an ability rework) — confirm the hero/
  ability reference data used is current-patch (per `dota-domain`'s data
  model guidance and `dota-data`'s reference-data versioning) before
  trusting mechanism-based reasoning as patch-accurate.

## Bracket-awareness requirements

- **MetaStrength should be bracket-segmented**, not computed from an
  all-bracket blend — a hero's practical strength genuinely differs by
  bracket (execution-dependent heroes often perform very differently
  between, say, Herald and Immortal, independent of theoretical power).
  Segment by the relevant bracket(s) for the player/team this
  recommendation is for, per `dota-data`'s rank-tier field guidance.
- **When bracket-specific data is sparse** (e.g. analyzing a very high or
  very low bracket with fewer games recorded), apply the same Bayesian-
  shrinkage discipline `dota-item-recommender`/`dota-ml` establish
  elsewhere — shrink toward a broader bracket-neighbor estimate rather
  than either trusting a noisy narrow estimate or silently ignoring
  bracket entirely.
- **LaneFit and execution-related factors are often the most
  bracket-sensitive** — a lane matchup that's "even" in theory can be
  lopsided in practice at a bracket where one side's mechanical execution
  reliably wins the exchange. Where possible, let observed bracket-level
  lane outcomes inform LaneFit, not just a theoretical matchup read,
  while still applying the same bias-avoidance discipline (lane outcomes
  are also subject to selection effects — e.g. some hero/lane assignments
  are chosen more by confident/experienced players in the first place).

## Where this data comes from

Both patch and bracket segmentation are `dota-data` pipeline
responsibilities (NORMALIZED-stage patch/timestamp attachment, rank-tier
handling) — this skill consumes correctly-segmented FEATURES/DATASET
output rather than re-deriving patch/bracket logic independently. If a
draft-analysis feature needs patch or bracket segmentation that the data
pipeline doesn't yet support at the needed granularity, that's a
`dota-data` gap to raise and fix upstream, not something to work around
locally with an approximation that silently reintroduces the exact
aggregation problem this section exists to prevent.

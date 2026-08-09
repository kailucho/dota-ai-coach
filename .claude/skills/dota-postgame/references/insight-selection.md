# Selecting 1-3 Actionable Insights

## Why this is a hard requirement, not a suggestion

Post-game analysis (per `analysis-dimensions.md`) can legitimately
compute dozens of statistics across laning, farm, item timings, deaths,
teamfights, objectives, buyback, power spikes, and turning points.
Presenting all of them is not more helpful than presenting a few — it's
the exact failure mode `dota-live-coach`'s core rule already names for
the live case ("shown everything, learns to ignore all of it"), just
manifesting as an overwhelming report instead of a spammy overlay. The
computation can and should be broad; the *presentation* must be narrow.

## What makes an insight a good candidate for one of the 1-3 slots

**Actionable.** The player should be able to do something differently
next time because of this insight — a vague or purely descriptive
observation ("your net worth graph looks like this") isn't actionable on
its own, even if it's accurate. Prefer insights that map to a specific,
repeatable decision the player can make differently (a timing to aim for,
a positioning pattern to watch for, a farm-priority conflict to
communicate about).

**Well-supported.** Per `sample-size-and-confidence.md`, a thin-sample
or purely single-game observation is a weaker candidate than a
comparison-group-backed association, all else equal — don't let novelty
or surprise value override evidentiary weight when picking the final 1-3.

**Correctly tiered.** Per `correlation-vs-causality.md`, know and can
honestly state which evidence tier the insight belongs to — an insight
that can only be honestly phrased with heavy causal-language caveats is
a weaker, more awkward choice than one that's cleanly statable at
whatever tier it actually is.

**Non-redundant with the others selected.** If multiple candidate
insights are really pointing at the same underlying issue (e.g. "late
BKB timing" and "died to burst damage repeatedly" might both trace back
to the same root cause), prefer surfacing the most fundamental/root-cause
version once, rather than using up multiple scarce slots restating
different symptoms of the same thing.

**Prioritized by impact.** Where multiple genuinely distinct, well-
supported, actionable candidates exist, prefer the ones most likely to
have mattered for this game's/this player's outcome — a `turning-points`-
dimension finding (per `analysis-dimensions.md`) that ties directly to a
game-deciding moment usually outranks a smaller efficiency finding, all
else equal.

## Where the rest of the computed statistics go

Everything not selected for the 1-3 headline insights should remain
available, not discarded — this is the post-game equivalent of
`dota-live-coach`'s "detail belongs in a pulled-up view, not the live
stream" principle:
- a drill-down/detail view the player can explore on their own
  initiative, organized by the `analysis-dimensions.md` categories;
- raw stats and comparison-group context available for a player who
  wants to dig deeper, without being forced into the headline report.

The distinction is the same as `dota-item-recommender`'s
`alternatives` field and `dota-draft`'s full six-factor breakdown — rich
detail computed and available, filtered presentation for what's pushed
to the player by default.

## A practical selection process

1. Compute candidate insights across all `analysis-dimensions.md`
   categories, each tagged with evidence tier
   (`correlation-vs-causality.md`) and confidence/sample-size
   (`sample-size-and-confidence.md`).
2. Filter out candidates too thin/low-confidence to state honestly and
   usefully (per `sample-size-and-confidence.md`'s thresholds).
3. Deduplicate candidates that trace to the same underlying root cause.
4. Rank remaining candidates by actionability and likely impact on this
   game's/player's outcome.
5. Select the top 1-3, and route everything else to the drill-down/
   detail surface rather than discarding it.

This process should be auditable (the full candidate list and why each
was included/excluded should be inspectable, not just the final 1-3
presented) — consistent with `dota-item-recommender`'s general preference
for structured, traceable reasoning over an opaque final output.

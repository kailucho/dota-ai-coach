# ADR 0019: Draft baseline (hero winrate + matchup matrix + logistic regression)

## Status

Accepted

## Context

Following the Win Probability baseline (`docs/adr/0017`/`0018`), the user
asked to continue Fase 3 with the draft prediction model. The full Draft
Coach described in `docs/MASTER_PLAN.md` (semantic hero vectors, synergy/
counter modeling, execution difficulty, damage-type distributions) needs
hand-curated hero data OpenDota doesn't expose and is out of scope for a
baseline. Per `.claude/skills/dota-ml`'s own baseline list for Draft:
*"hero global winrate; matchup matrix; logistic regression; LightGBM"* —
this ADR covers exactly that reduced scope, confirmed with the user
before building.

## Decision

`tools/DotaAiCoach.Training/src/dota_training/models/draft_baseline.py`,
reusing the same 1863-match dataset and 4-way temporal split discipline
as the win-probability model (`stages/draft_dataset.py`, one row per
match rather than per timestamp — no `T` dimension exists for a draft-only
prediction).

**Two features**: `hero_winrate_diff` (Radiant's average hero win rate
minus Dire's, per hero-patch, **train-split only**) and
`matchup_advantage` (mean pairwise hero-vs-hero win rate across all 25
Radiant×Dire hero pairs, minus the train-split overall prior).

**Both tables use Bayesian shrinkage toward the train-split overall win
rate** — `HERO_WINRATE_PRIOR_STRENGTH = 30`, `MATCHUP_PRIOR_STRENGTH = 10`
pseudo-observations. No LightGBM: with only 2 engineered features, there
is no meaningful non-linear interaction for a tree ensemble to exploit,
and `dota-ml` explicitly warns against reaching for a bigger model without
a concrete reason to expect it helps.

## A real bug found and fixed during this pass: unshrunk hero winrates

The first version of `compute_hero_global_winrate` computed the raw
empirical win rate per (hero, patch) with **no shrinkage at all** — the
exact "won 8/10 games, noisy and overconfident" failure mode
`dota-ml/references/model-selection.md` names explicitly as the reason
shrinkage exists. Concretely: hero id 80 showed a 100% win rate on 71
train games, hero id 3 showed 0% on 23 games. This directly caused the
domain sanity check (a synthetic top-5-winrate hero pool should beat a
bottom-5 pool) to **fail** — the model actually predicted the top-5 pool
would win *less* (49.5%) than the bottom-5 pool (65.0%), the opposite of
the obviously-correct direction.

Fixed by shrinking the hero table the same way the matchup matrix was
already shrunk (`(wins + overall_winrate * k) / (games + k)`, `k=30`).
After the fix, the sanity check passes decisively: top-5 pool wins 79.1%
of the time against a bottom-5 pool, bottom-5 wins 33.4% — the correct
direction with real separation. Caught by re-running the domain sanity
check, not assumed — this is exactly the value of the check per
`dota-ml/references/evaluation.md`'s "construct hand-built scenarios with
an obvious expected direction" requirement, applied to a new model.

Regression tests added (`tests/test_draft_baseline.py`): a small-sample
hero winrate must be pulled well below its raw empirical value, a
large-sample one must stay close to it (confirming the shrinkage
constant doesn't over-smooth real signal), and the top5-vs-bottom5
domain check is now a direct, always-run regression test rather than
something only checked by hand.

## Results — an honest, weak-signal finding

| | AUC-ROC | log loss | Brier |
|---|---|---|---|
| Constant train-prior baseline | 0.500 | 0.691 | 0.249 |
| Single-feature LogReg (`hero_winrate_diff` alone) | 0.481 | 0.697 | 0.252 |
| **Chosen model (LogisticRegression, both features) — val** | 0.508 | 1.326 | 0.372 |
| **Final test (touched once)** | **0.531** | **0.817** | **0.251** |

**Draft alone barely predicts match outcome in this dataset** — test AUC
0.531 is only marginally above chance (0.5), and the single-feature
baseline (0.481) is actually *below* chance on val, meaning
`hero_winrate_diff` alone is not a reliable signal at this sample size
(1117 train matches spread across 127 heroes, median 75 games/hero after
splitting into a Radiant/Dire role — thin per-hero-per-role samples even
after shrinkage). This is a **genuinely expected result for pub-bracket
ranked Dota**, not a bug: draft quality matters far less than in-game
execution at most skill brackets, which is exactly why the Win
Probability model (which observes live match state) achieves AUC 0.839
while this draft-only model achieves 0.531. Reported honestly rather than
tuned/hidden — a draft baseline this weak is itself the useful finding:
a future draft-recommendation feature should not overweight raw win-rate
signal, and should probably lean more heavily on comfort/matchup-style
signals (as `docs/MASTER_PLAN.md`'s fuller Draft Coach design already
anticipates) than on a global-winrate model like this one.

`hero_winrate_diff`'s coefficient is negative in the combined model
(-0.78) despite the feature itself trending positively with outcome in
isolation — a sign the two features are fighting each other on this small
a sample (`corr(hero_winrate_diff, matchup_advantage) = 0.56` on train).
Investigated whether this indicated leakage (a feature correlating 0.87
with the label on train initially looked alarming) — confirmed it was
**not** leakage by checking the same correlation on held-out val data,
where it drops to ~0.00, meaning the strong train correlation was
overfitting to the small, sparse matchup table, not a computation bug.
This is exactly why `dota-ml/references/leakage-and-feature-availability.md`'s
ablation-sanity guidance says to *investigate* an implausibly strong
feature rather than either trusting or dismissing it outright.

## Consequences

- Model saved at `models/draft-baseline-v1.0-<runid>/` with the same
  `metadata.json`/`evaluation.json` reproducibility contract as the win-
  probability model. FairPlay: no `POSTGAME_ONLY` features (draft, patch,
  and outcome are all pre-match/`PublicPreflight`-eligible information),
  but still not wired into the live app in this pass.
- This baseline should **not** be used as-is for any real recommendation
  — its predictive power is too weak. It exists as the honest, required
  baseline `dota-ml` mandates before any more complex draft model is
  attempted, and as a concrete data point for scoping that future model
  (more training data, richer features per `docs/MASTER_PLAN.md`'s
  fuller representation, or both, will likely be necessary).
- `tests/test_draft_baseline.py`'s regression tests permanently guard
  against the unshrunk-winrate bug recurring.

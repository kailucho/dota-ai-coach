# ADR 0017: Win Probability baseline and Python training pipeline

## Status

Accepted

## Context

Following Fase 2 (Player Profile), the user requested continuing into
Fase 3 of `docs/MASTER_PLAN.md` — offline ML baselines. Fase 3's full
scope (draft baseline, win probability, item ranker, ONNX export, live
inference wiring) is a multi-week project; the user explicitly scoped
this iteration down to **one model**: a Win Probability baseline
(`P(win | player-visible state at timestamp T)`), built, trained, and
evaluated — no ONNX export, no live wiring, no other models.

`.claude/skills/dota-ml` mandates baseline-first modeling, temporal
validation, automated leakage tests, calibration, and a reproducibility
record before any model is considered done. `.claude/skills/dota-data`
mandates the RAW→BRONZE→NORMALIZED→FEATURES→DATASET pipeline with eight
data-quality rules. Both applied in full to this scoped slice.

## Decision 1: Python, under `tools/DotaAiCoach.Training/`, outside the .NET solution

Per `dota-architecture`'s explicit allowance for `Training` to use a
different toolchain. A dedicated venv (`requirements.txt` pinned via
`pip freeze`), never added to `DotaAiCoach.sln`. The rest of the .NET
solution is untouched by this work.

## Decision 2: Parquet + DuckDB-compatible layout, not SQLite

Closes the deferral left open by `docs/adr/0011` ("SQLite deferred to
Fase 3's bulk ingestion pipeline"). RAW is JSON envelopes on disk in the
same format `RawResponseCache.cs` already uses
(`{endpoint,fetchedAtUtc,statusCode,sourceName,schemaVersion,body,isParsed}`),
readable from both C# and Python. BRONZE/NORMALIZED/FEATURES/DATASET are
Parquet files, queried directly with pandas/DuckDB-style tooling — no
database server, consistent with this being a local-first project.

## Decision 3: critical data-quality correction — `game_mode` 22 is Ranked, not Turbo

An earlier assumption (carried from this project's own `dota-data` skill
notes) held that `game_mode==22` was Turbo. **Verified live against
`https://api.opendota.com/api/constants/game_mode`**:
`{"22": "game_mode_all_draft", "23": "game_mode_turbo"}`. Turbo is 23.
This was caught before any training happened, but is exactly the kind of
silent dataset-poisoning error `dota-data`'s rule 1 (never mix Turbo with
Ranked by default) warns against — mixing Turbo into a "ranked" dataset
would have taught the model a blended average of two different games,
with no crash or visible symptom.

The correction is enforced as a regression test
(`tests/test_data_quality.py::TestGameModeCorrection`), with an explicit
comment at the `RANKED_GAME_MODE = 22` constant citing this ADR, so it
cannot be silently reverted.

## Decision 4: data acquisition — a 3-step funnel, all verified live

1. `GET /parsedMatches?less_than_match_id=` walked backwards from several
   `match_id` anchors — free, returns already-parsed match IDs.
2. `GET /explorer?sql=...` in batches of ~500 IDs against `public_matches`,
   filtering `lobby_type=7 AND game_mode=22 AND duration BETWEEN 900 AND
   4500` — ~1 request per 500 candidates, also returns `avg_rank_tier`.
3. `GET /matches/{id}` for survivors only — 1 request each, already
   parsed (no `POST /request` + poll cost).

Rejected alternatives (verified, not assumed): `/publicMatches` returns
in-progress matches and its `min_rank`/`max_rank` params are silently
ignored; seeding from known account IDs biases toward those players'
match history; the historical `matches` table is 100% professional/league
Captains Mode, the wrong population for a pub win-probability model.

Anchors were spread across 7 `match_id` values corresponding to roughly
Dec 2025 through Aug 2026 (verified via direct `start_time` probing), so
the resulting dataset spans real calendar time rather than one session's
worth. Measured yield: 19–78% ranked-survivor rate per anchor (improving
in more recent anchors), landing **1912 raw matches, 1863 after excluding
49 abandoned matches** (target was 2000; documented honestly rather than
padded).

A real transient issue was hit and fixed during acquisition: the
unauthenticated rate limit is tighter in practice than the configured
55/min (a live 429 was observed), and OpenDota occasionally 500s on an
individual match (3 out of 1915 fetch attempts). The client was hardened
with exponential backoff on 429 and per-match error skipping (one bad
match must not lose an entire run's progress) — both are real, not
hypothetical, fixes verified by rerunning acquisition to completion.

## Decision 5: feature set, timestamp-bound, with an explicit FairPlay spec

One row per `(match, T)` for `T ∈ {5,10,...,40}` minutes (only if
`T ≤ duration_minutes - 2`), Radiant-perspective label. ~12,253 rows from
1863 matches. Every feature computed using only event/array data with
`time`/index `≤ T` — verified mechanically by
`tests/test_leakage.py::test_feature_at_T_ignores_data_after_T`, which
truncates a real captured match to "only what existed at T" and asserts
every computed feature is unchanged versus the full-match computation.

`src/dota_training/features_spec.yaml` is the Python-side parallel to the
C# `[FairPlaySource]` attribute (unimportable from Python), documenting
`source`/`availability`/`visibility`/`fairPlayClassification`/
`justification` per feature. Per the user's explicit choice, this
baseline **includes `POSTGAME_ONLY` features** (enemy exact gold, enemy
item purchase timing) for the strongest possible baseline signal — the
spec keeps the `LIVE_ALLOWED` subset mechanically selectable for a future
live-eligible variant, and `ProfileFairPlayTests`-equivalent discipline
(`tests/test_leakage.py::TestFeaturesSpecCoverage`) asserts the subset is
non-empty and no feature is undeclared.

`hero_pick_winrate_diff` is computed from the **train split only**,
joined by **each match's own patch** (never "the latest patch") —
verified by `tests/test_patch_pinning.py` with an adversarial fixture
where the same hero has opposite win rates on two different patches.

## Decision 6: model progression — Logistic Regression, then LightGBM, no further

Per `dota-ml`'s mandate. Both trained; **LightGBM did not beat Logistic
Regression by the 0.01 AUC threshold** (measured gain: -0.006 to -0.008
AUC across two runs) — the pipeline automatically kept the simpler,
more interpretable Logistic Regression model, exactly the "permission to
stop at the baseline" outcome the skill describes as a genuinely good
result, not a disappointment. No Transformer/GNN/deep model was
considered; none is justified at this data scale.

## Decision 7: four-way temporal split, calibration on a third split, test touched once

`train`/`val`/`calib`/`test` at 60/15/10/15% of matches, ordered strictly
by `match_id` (verified monotonic with `start_time` live), split by
match so no match's several `(match, T)` rows straddle a boundary.
Calibration (isotonic regression) fit on `calib`, checked on `val`
(pre-calibration ECE 0.067 → post-calibration 0.044). The `test` split's
metrics were computed exactly once, in the final training run, per the
skill's explicit rule against repeatedly peeking at it during iteration.

## Results (this baseline's actual numbers)

| | AUC-ROC | log loss | Brier |
|---|---|---|---|
| Constant 0.5 baseline | 0.500 | 0.693 | 0.250 |
| Single-feature LogReg (`gold_adv_at_T`) | 0.793 | 0.547 | 0.186 |
| **Chosen model (LogisticRegression) — val** | 0.741 | 0.615 | 0.210 |
| LightGBM (not chosen) — val | 0.734 | 0.642 | 0.219 |
| **Final test (touched once)** | **0.777** | **0.564** | **0.192** |

Notable: the full-feature Logistic Regression scored *lower* on val AUC
than the single-feature `gold_adv_at_T` baseline alone (0.741 vs 0.793).
This is a real, honestly-reported result, not hidden — it suggests
several of the added features are adding noise rather than signal for
this model class/dataset size, worth investigating before extending this
baseline further (candidate cause: 18 features on ~7,300 training rows
with several weakly-correlated combat/objective features).

Segment results confirm the pattern `dota-ml`'s evaluation guidance
predicts: early-game AUC 0.674, mid-game 0.836, late-game 0.868 — the
model is weak early (sparse signal) and strong late (more accumulated
state), which matters directly for a live coaching tool. By-patch
segmentation is **not meaningful** — all 1863 matches landed on a single
patch (60), confirming the anticipated limitation. By-hero segmentation
produced 79 heroes with ≥100 rows (AUC range ~0.65–0.87). By-rank-tier
segmentation: Ancient+ (n=1543, AUC 0.786), Archon-Legend (n=227, AUC
0.717), Herald-Crusader underpowered (n=89, correctly omitted).

Domain sanity checks: gold-advantage monotonicity **passed** cleanly
(probabilities strictly increasing from 0.36 to 0.81 across a ±10k gold
sweep); near-even-at-zero **passed** (0.61, within the 0.15 tolerance of
0.5); symmetry check **failed** (p=0.734 for +5000/+3000 gold/xp vs.
1-p=0.540 for the negated case) — recorded honestly rather than
suppressed; worth investigating in a future iteration (candidate cause:
`gold_gini_radiant`/`gold_gini_dire` aren't meaningfully "negatable" the
same way `_diff` features are, since gini is never negative, which may
be distorting the symmetry probe itself rather than the model).

## Consequences

- Model artifact saved at
  `tools/DotaAiCoach.Training/models/win-probability-v1.0-20260809T163642Z/`
  with `metadata.json` (full reproducibility record: data version, code
  commit, hyperparameters, seed, library versions) and `evaluation.json`
  (all metrics/segments/sanity checks above) — both committed to git;
  `.pkl` model/calibrator files are gitignored as regenerable.
- **Explicitly out of scope, not started**: ONNX export, wiring into
  `RecommendationEngines`/`Ml.Inference`/the overlay, draft baseline,
  item ranker, any model beyond Logistic Regression + LightGBM. This
  model is research-only per its `POSTGAME_ONLY` features and is not
  live-eligible without a retrain restricted to the `LIVE_ALLOWED`
  feature subset.
- Known limitations, stated rather than hidden: 1863 matches is a thin
  slice of the total ranked population; all data is single-patch, so
  patch-robustness is untested; the symmetry sanity check failed and
  should be revisited; the full feature set underperforms the
  single-feature baseline on val AUC, suggesting feature pruning or
  regularization is worth investigating in a follow-up.
- Recommended next step per `docs/ROADMAP.md`: either (a) investigate the
  feature-set regression against the single-feature baseline before
  extending this model further, or (b) begin the full `dota-data`
  RAW→BRONZE→NORMALIZED→FEATURES→DATASET pipeline for a genuinely
  production-scale dataset, at which point this baseline's RAW cache
  becomes a small seed rather than being discarded.

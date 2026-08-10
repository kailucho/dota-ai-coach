# ADR 0022: LIVE_ALLOWED Win Probability retrain and ONNX export of all three baselines

## Status

Accepted

## Context

`docs/adr/0021`'s "Recommended next phase" (also restated in
`docs/ROADMAP.md`) named this task explicitly: retrain the Win
Probability model restricted to `features_spec.yaml`'s
`LIVE_ALLOWED`/`PUBLIC_PREFLIGHT` feature subset, then export all three
Fase 3 models (Win Probability, Draft, Item Ranker) to ONNX and validate
the exports, ahead of eventually wiring them into `DotaAiCoach.MlInference`
(per `dota-architecture`'s `references/dotnet-solution-structure.md`).
This task is scoped as **export + validate only** — no `.NET`/`Ml.Inference`
wiring in this pass.

Per `.claude/skills/dota-ml` (all four references read in full) and
`.claude/skills/dota-fairplay`, both loaded before any code was written.

## Decision 1: a sibling module, not a parameterization of `train.py`

`train.py` is the documented `POSTGAME_ONLY` research baseline referenced
as-is by `docs/adr/0017`/`0018`/`0021` — it stays intact and buildable.
Rather than adding a `feature_set` flag to it, a new sibling module
`train_live.py` was added, reusing `calibrate.py`/`evaluate.py` and
mirroring `train.py`'s exact structure (baselines, C-grid-searched
LogReg, LightGBM comparison, calibration on `calib`, test touched once,
full segmentation, domain sanity checks). The two variants diverge in
more than a feature list — different `model_id` prefix
(`win-probability-live-` vs `win-probability-`), a different `fairplay`
metadata block, and (this pass) a different ONNX export path — so
duplicating the training-loop structure as a second file was judged
clearer than threading a conditional through one file for two genuinely
different artifacts. Both files import the same `calibrate.py`/
`evaluate.py` helpers, so the actual validation discipline cannot drift
between them silently.

## Decision 2: verified feature set against the live `features_spec.yaml`, not the task brief's summary

The task brief listed the expected `LIVE_ALLOWED`/`PUBLIC_PREFLIGHT` vs
`POSTGAME_ONLY` split; this was independently re-verified by reading
`features_spec.yaml` directly rather than trusted verbatim, per this
project's live-verification discipline (`docs/adr/0017` decision 3,
`docs/adr/0021` decision 1). The brief's list was accurate. Final
`train_live.py` `FEATURE_COLUMNS` (12 features, all `LIVE_ALLOWED` or
`PUBLIC_PREFLIGHT`):

```
gold_adv_at_T, xp_adv_at_T, gold_adv_slope_5, xp_adv_slope_5,
kills_diff_at_T, deaths_in_last_5min_diff,
towers_killed_diff, barracks_killed_diff, roshan_killed_diff,
hero_pick_winrate_diff, avg_rank_tier, minute_T
```

Plus `first_blood_radiant` (`LIVE_ALLOWED`, boolean, encoded separately
from `FEATURE_COLUMNS` — same treatment as `train.py`). `patch`
(`PUBLIC_PREFLIGHT`) is used only as a join key for
`hero_pick_winrate_diff` and a segmentation dimension, not as a direct
model input, matching `train.py`'s existing treatment.

Excluded, all confirmed `POSTGAME_ONLY` in `features_spec.yaml`:
`lh_diff_at_T`, `gold_gini_radiant`, `gold_gini_dire`,
`max_hero_gold_diff`, `teamfights_won_diff`, `key_item_count_diff`.

A mechanical FairPlay classification test
(`tests/test_train_live_and_onnx.py::TestLiveFeatureSetClassification`)
asserts this at the code level, per
`leakage-and-feature-availability.md`'s explicit "structural/mechanical
test, not a manual review checklist item" requirement — it will fail if
a future edit reintroduces a `POSTGAME_ONLY` feature into
`train_live.py`, or if `features_spec.yaml` silently reclassifies one of
the six excluded features instead of the model actually dropping it.

## Results — Win Probability, LIVE_ALLOWED retrain

Trained on the same dataset/split as `docs/adr/0021`'s rebuild
(`data/datasets/ranked-ad-2760-20260810`, train/val/calib/test, test
touched exactly once).

| | AUC-ROC | log loss | Brier |
|---|---|---|---|
| Constant 0.5 baseline (val) | 0.500 | 0.693 | 0.250 |
| Single-feature LogReg (`gold_adv_at_T`, val) | 0.787 | 0.557 | 0.189 |
| Chosen model (LogisticRegression, C=1e-6, val) | 0.777 | 0.689 | 0.248 |
| LightGBM (not chosen, val) | 0.702 | 0.788 | 0.249 |
| **Final test (touched once)** | **0.813** | **0.558** | **0.176** |

**Honest result: test AUC 0.813 vs. the research (full-feature,
`POSTGAME_ONLY`-inclusive) model's 0.819 — a real but small decline
(-0.006), smaller than initially expected.** The task brief anticipated
a larger gap, reasoning that the dropped `POSTGAME_ONLY` features
(`gold_gini_radiant`/`gold_gini_dire`/`max_hero_gold_diff`/
`teamfights_won_diff`) were "presumably adding real signal." That
reasoning was sound in direction but the magnitude turned out smaller
than presumed: `lh_diff_at_T` and `key_item_count_diff` were also
dropped and contribute little (both are close proxies for information
already captured by `gold_adv_at_T`/`xp_adv_at_T`, which remain), and the
four genuinely-informative dropped features apparently carry less
marginal signal once the strong economic-advantage/objective features
are already present — consistent with `docs/adr/0018`'s multicollinearity
finding that these features are correlated with each other in the first
place. This is reported plainly rather than reframed as a bigger or
smaller win than it is.

The same `C` grid search as `train.py` (candidates
`[1.0, 0.1, 0.01, 0.001, 1e-4, 1e-5, 1e-6]`) again selected the
strongest available regularization (`C=1e-6`) — the collinearity finding
from `docs/adr/0018` (economic-advantage features correlating
0.63-0.91) transfers to this smaller feature set, since
`gold_adv_at_T`/`xp_adv_at_T`/their `_slope_5` variants are still all
present and still collinear with each other. LightGBM again did not beat
Logistic Regression (val AUC gain -0.075) — the pipeline correctly kept
the simpler model, same outcome as every prior ADR in this series.

Segmented results (test): early-game (5-15min) AUC 0.736, mid-game
(20-30min) AUC 0.863, late-game (35+min) AUC 0.869 — the same
early-weak/late-strong pattern as `docs/adr/0017`/`0018`/`0021`, still
holding. By rank tier: Ancient+ (n=2311) AUC 0.821,
Archon-Legend (n=353) AUC 0.771, Herald-Crusader (n=89) correctly
omitted as underpowered. By-patch (test): only patch 60 present
(n=2753, AUC 0.813) — same structural limitation as `docs/adr/0021`
decision 6 (patches 58/59 are entirely in `train` under the temporal
split). All three domain sanity checks pass (monotonicity,
near-even-at-zero, symmetry); the gold-advantage sweep is flat
(0.518-0.520, same order as `docs/adr/0021`'s 0.518-0.520 for the
research model at the same `C=1e-6`) — expected given the equally strong
regularization, not a new finding.

New artifact:
`tools/DotaAiCoach.Training/models/win-probability-live-v1.0-20260810T034501Z/`,
with the standard `metadata.json`/`evaluation.json` contract plus a
`fairplay` block explicitly recording `feature_set: live_allowed` and
the six excluded `POSTGAME_ONLY` features by name.

## Decision 3: ONNX export approach per model type

- **Win Probability (live) and Draft baseline** (both
  `StandardScaler` + `LogisticRegression`): wrapped in an
  `sklearn.pipeline.Pipeline(scaler, logreg)` and exported as a single
  ONNX graph via `skl2onnx` (`skl2onnx==1.20.0`, target opset 17), with
  `zipmap` disabled so the output is a plain float tensor rather than a
  list of Python dicts. This puts the scaler inside the exported graph
  per `onnx-export.md`'s explicit preference — `Ml.Inference` will not
  need a second, independently-maintained scaling implementation for
  either model.
- **Item Ranker** (`LightGBM` `LGBMRanker`): exported via
  `onnxmltools.convert_lightgbm` (`onnxmltools==1.16.0`). No
  preprocessing needed embedding — its 5 input features
  (`minute`, `n_items_owned_before`, and three popularity-table lookups)
  are already numeric by construction. **Important scope note**: the
  popularity tables themselves (`tables['global'|'hero'|'hero_minute']`
  in `model.pkl`, the Bayesian-shrunk lookup dictionaries `item_ranker.py`
  computes) are **not** part of the ONNX graph — they're a
  train-split-derived lookup structure, not a differentiable model
  component, and `skl2onnx`/`onnxmltools` have no standard op for an
  arbitrary Python dict lookup. This is flagged explicitly in
  `onnx_export_metadata.json` (`popularity_tables_not_exported_note`) as
  something the live-wiring step's Feature Engine will need to load
  separately (e.g. export the three tables to a small JSON/Parquet
  artifact and replicate the lookup-with-fallback-to-parent-table logic
  in C#) — out of scope for this export-only pass.
- **Isotonic calibration** (Win Probability-live, Draft baseline — Item
  Ranker has no calibration step, it's a ranking task): confirmed **not
  representable as a standard ONNX op** and **not embedded in the
  graph**, per the brief's explicit fallback instruction. Instead,
  `IsotonicRegression`'s fitted state was inspected directly: it reduces
  to a monotone piecewise-linear function over its fitted
  `(X_thresholds_, y_thresholds_)` breakpoints with `out_of_bounds="clip"`
  — a small, literal, non-black-box table (13-40 points depending on the
  model), not tuned code. This table is written verbatim into each
  model's `onnx_export_metadata.json` (`calibration_replication` field)
  with the exact reconstruction recipe (`clip` to the threshold range,
  then `np.interp`/equivalent linear interpolation) — reproducible in C#
  without any Python/scikit-learn dependency at inference time.

Version tagging: each `onnx_export_metadata.json` records
`source_model_id` and `source_metadata_ref` pointing back to the exact
`metadata.json` that produced the pre-export model, satisfying
`reproducibility-and-versioning.md`'s traceability requirement for the
ONNX artifact specifically (not just the `.pkl`).

Two different `onnxmltools`/`skl2onnx` package versions support
different maximum opsets with the versions actually installed in this
project's venv (verified live by a `RuntimeError` at opset 17 for
LightGBM): the two `skl2onnx` exports use opset 17, the `onnxmltools`
LightGBM export uses opset 15. This is recorded explicitly per-model in
`onnx_export_metadata.json` rather than silently forcing both to the
lower common value, since the two exporters are independent code paths
with no shared-opset requirement between them.

## Decision 4: full validation checklist run and passed, all three models

Per `onnx-export.md`'s checklist, for each of the three models:

- **Fixed test inputs including edge cases** (all-zero/missing-feature
  fill, extreme large positive/negative, mid-boundary, two representative
  random rows) run through both the pre-export and exported model.
  Results (max absolute difference across all test rows):

  | model | max abs diff | tolerance | passed |
  |---|---|---|---|
  | win-probability-live | 6.91e-08 | 1e-4 | yes |
  | draft-baseline | 1.09e-08 | 1e-4 | yes |
  | item-ranker | 3.30e-06 | 1e-3 | yes |

  All three are far inside tolerance — essentially float32/float64
  rounding noise, no export-introduced behavioral drift found.

- **Input schema documented explicitly** (feature names, order, dtype)
  in each `onnx_export_metadata.json`'s `input_schema` field, matching
  `bundle["feature_columns"]`'s exact order from the source `model.pkl`
  — a silent feature-order mismatch is exactly the failure mode
  `onnx-export.md` warns is hard to detect, so this is recorded as
  literal data, not prose description.

- **Calibration re-checked against the exported model's own outputs**,
  on real `val`-split data (not just the synthetic fixed-input parity
  check above), for both LogReg models:

  | model | n | ECE (calibrated ONNX outputs) | AUC (calibrated ONNX outputs) |
  |---|---|---|---|
  | win-probability-live | 2738 | 0.0375 | 0.7754 |
  | draft-baseline | 414 | 0.0115 | 0.5009 |

  Both match their respective training-time `evaluation.json` values
  within float32 tolerance, confirming the exported graph did not shift
  the output distribution. (Item Ranker has no calibration step to
  recheck — it is a ranking model evaluated by NDCG/MRR, not a
  calibrated probability, per `item_ranker.py`.)

- **Version traceability** confirmed via `source_model_id`/
  `source_metadata_ref` (Decision 3, above).

- **Inference latency** (this dev machine, `CPUExecutionProvider`,
  single-row inference averaged over 200 calls — a rough sanity figure,
  not a rigorous benchmark, per the brief):

  | model | latency (ms/call) |
  |---|---|
  | win-probability-live | 0.052 |
  | draft-baseline | 0.036 |
  | item-ranker | 0.039 |

  All three are roughly two orders of magnitude under the single-digit-
  millisecond expectation for a live overlay use case — not a concern.

New artifacts (per model, alongside the existing `metadata.json`/
`evaluation.json`, no existing file touched or deleted):
- `models/win-probability-live-v1.0-20260810T034501Z/model.onnx` +
  `onnx_export_metadata.json`
- `models/draft-baseline-v1.0-20260810T032042Z/model.onnx` +
  `onnx_export_metadata.json`
- `models/item-ranker-v1.0-20260810T032341Z/model.onnx` +
  `onnx_export_metadata.json`

Draft baseline and Item Ranker used the existing, already-trained
`-v1.0-20260810T032xxxZ` artifacts from `docs/adr/0021`'s rebuild — no
retrain was needed or performed for either, per the task's scope (only
Win Probability needed a new LIVE_ALLOWED retrain; Draft and Item Ranker
already contain zero/near-zero `POSTGAME_ONLY` features).

## Consequences

- `tools/DotaAiCoach.Training/src/dota_training/models/train_live.py`:
  new, the LIVE_ALLOWED-restricted Win Probability training entrypoint.
  `train.py` is completely unmodified.
- `tools/DotaAiCoach.Training/src/dota_training/models/export_onnx.py`:
  new, handles all three export paths (`export_logreg_pipeline` for
  scikit-learn LogReg+Scaler pipelines, `export_lightgbm_ranker` for
  LightGBM), plus the shared validation checklist logic.
- `tools/DotaAiCoach.Training/requirements.txt`: regenerated via
  `pip freeze`, adding `onnx==1.22.0`, `onnxmltools==1.16.0`,
  `onnxruntime==1.28.0`, `skl2onnx==1.20.0` and their transitive
  dependencies (`flatbuffers`, `ml_dtypes`, `protobuf`,
  `typing_extensions`) — all were already present in the project's venv,
  so this pass only recorded them, it did not newly install anything.
- `tools/DotaAiCoach.Training/tests/test_train_live_and_onnx.py`: new,
  9 tests — mechanical FairPlay classification tests for
  `train_live.py`'s feature set (asserting live-eligibility and the
  absence of the six known `POSTGAME_ONLY` features), a regression guard
  confirming `train.py`'s research feature set is unaffected, and two
  genuine automated ONNX-vs-original parity tests (one for a
  scikit-learn pipeline, one for a LightGBM ranker) built against
  small in-test-trained models so they don't depend on any specific
  trained artifact remaining on disk across future retrains.
- Full test suite: **59 tests, all passing** (50 pre-existing + 9 new),
  no existing test needed to change.
- **New model artifact** (kept alongside, nothing deleted/overwritten):
  `models/win-probability-live-v1.0-20260810T034501Z/`.
- **Known limitations, reported honestly**:
  1. The AUC decline from the live restriction (0.819 → 0.813) is real
     but smaller than the task brief anticipated — a genuine, useful
     finding about which dropped features actually mattered (see
     Decision 2's discussion), not a discrepancy to paper over.
  2. Item Ranker's popularity lookup tables are not part of its ONNX
     graph (Decision 3) — the live-wiring step must export/replicate
     these separately; this was already flagged as a gap in
     `docs/adr/0020`'s "no gold-affordability filter" note and is now
     flagged again specifically for the ONNX consumer.
  3. This ADR is **export + validate only** — no `Ml.Inference` C#
     project exists yet, and none of these three ONNX files are wired
     into any recommendation engine or the overlay. That is explicitly
     the next step (see `docs/ROADMAP.md`'s updated "Recommended next
     phase").
  4. Draft baseline's underlying predictive power (test AUC ~0.51,
     per `docs/adr/0021`) has not changed by this pass — exporting a
     weak model to ONNX does not make it a stronger model; it is
     export-validated but still not a strong candidate for actually
     driving a live draft recommendation without the richer feature
     work `docs/adr/0019`/`0021` already flagged as needed.
  5. By-patch test-set segmentation for the new live model has the
     same structural limitation `docs/adr/0021` decision 6 already
     documented (only patch 60 appears in `test` under the temporal
     split) — not re-litigated here, just inherited unchanged.

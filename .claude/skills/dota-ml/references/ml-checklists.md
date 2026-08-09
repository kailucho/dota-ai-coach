# ML Checklists

Three checklists for three different points in a model's lifecycle.

## Pre-training checklist

Run before starting a real training run (baseline runs are exempt from
some of these, since establishing the baseline is itself part of this
process — but a baseline still needs the feature-availability and
leakage checks).

- [ ] **Task framing confirmed** — is this actually a classification,
  regression, ranking, or shrinkage-estimation problem? (`model-selection.md`)
- [ ] **Baseline exists or is being built first** — a Logistic Regression
  (or equivalent simple baseline) result is available or is the first
  thing being trained, before any more complex model
  (`model-selection.md`).
- [ ] **Feature set is FairPlay-classified** — every feature has a
  documented `source`/`availability`/`visibility`/`fairPlayClassification`/
  `justification`, and none are `POSTGAME_ONLY`/`SPECTATOR_ONLY`/
  `FORBIDDEN` if this is a live model (`leakage-and-feature-availability.md`,
  `dota-fairplay`).
- [ ] **Features are timestamp-bound correctly** — every feature is
  computed using only data available as of its intended inference
  timestamp, verified by the timestamp-bound feature computation test,
  not just reasoned through (`leakage-and-feature-availability.md`).
- [ ] **Reference-data joins are patch-pinned** — joins against item/hero
  static data use the match's own patch, not "latest"
  (`leakage-and-feature-availability.md`).
- [ ] **Data source is a versioned `dota-data` DATASET build**, not an ad
  hoc query — see `dota-data`'s pipeline; training against unversioned
  data breaks reproducibility before training even starts.

## Pre-ship checklist

Run before a model is considered ready to version and hand off toward
live deployment (or before finalizing any model whose results will be
relied upon, live or offline).

- [ ] **Baseline comparison is documented with actual numbers**, not just
  "we tried a simpler model and this one was better" — record both
  results (`model-selection.md`).
- [ ] **If a complex architecture was used, its justification is
  documented concretely** — what signal the baseline structurally missed,
  and the measured improvement (`model-selection.md`).
- [ ] **Temporal validation was used throughout**, not just for the final
  split — confirm no k-fold or random-split leakage crept in during
  iteration (`validation-and-calibration.md`).
- [ ] **Final held-out test set was touched exactly once**, at the end —
  confirm it wasn't used for any iteration/tuning decisions
  (`validation-and-calibration.md`).
- [ ] **Calibration was checked on held-out data** and, if needed, fixed
  (`validation-and-calibration.md`).
- [ ] **Evaluation is segmented**, not just an aggregate metric — checked
  by game phase / bracket / patch / hero-role as relevant to the task
  (`evaluation.md`).
- [ ] **Domain sanity checks pass** — constructed test scenarios produce
  strategically sensible outputs per `dota-domain` (`evaluation.md`).
- [ ] **Leakage tests pass as automated tests**, not just a manual
  read-through — the timestamp-bound and FairPlay-classification tests
  from `leakage-and-feature-availability.md` are in the test suite and
  green.
- [ ] **Reproducibility record is complete** — data version, code
  version, hyperparameters, seed, library versions all recorded
  (`reproducibility-and-versioning.md`).
- [ ] **Model version identifier assigned** following the project's
  versioning scheme (`reproducibility-and-versioning.md`).

## Live-deployment checklist

Run specifically before a model goes into the live, in-game recommendation
path (i.e. is wired into `Ml.Inference`/`RecommendationEngines` per
`dota-architecture`) — additive to the pre-ship checklist above, not a
replacement for it.

- [ ] **ONNX export validated** per `onnx-export.md`'s full checklist
  (output parity, schema match, calibration re-check post-export,
  latency check on representative hardware).
- [ ] **`RecommendationEngines` consumes the model by explicit version**,
  not an unversioned "latest" reference (`reproducibility-and-versioning.md`,
  `dota-architecture`).
- [ ] **FairPlay test suite covers this model's live inputs end-to-end** —
  not just the training-time feature classification, but the actual
  live feature-supply path feeding this model at runtime
  (`dota-fairplay`, `leakage-and-feature-availability.md`).
- [ ] **A rollback path exists** — if this model's live behavior turns
  out to be wrong post-ship, is there a previous versioned model that can
  be swapped back in without a full retrain-and-redeploy cycle?
- [ ] **Explanation-layer sanity check** — if an LLM explains this
  model's outputs to the player (per "ML decides, LLM explains" in
  `SKILL.md`), spot-check that explanations for a range of model outputs
  (including low-confidence/uncertain ones) accurately reflect what the
  model actually output, rather than the LLM independently reasoning past
  the model's decision.

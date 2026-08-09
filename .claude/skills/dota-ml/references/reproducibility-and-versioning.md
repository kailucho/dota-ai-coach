# Reproducibility and Model Versioning

## Why this matters more here than in a typical ML side project

When a live recommendation looks strategically wrong (per `dota-domain`)
or a FairPlay concern surfaces about a shipped model, the first question
is always "what exactly produced this model" — which data, which code,
which hyperparameters. Without reproducibility and versioning, that
question is unanswerable, and the only fix is retraining blind and hoping
the issue doesn't recur. Treat this as debugging infrastructure, not
paperwork.

## What "reproducible" means concretely

A training run is reproducible if, given the same recorded inputs, it
produces an equivalent model. Concretely, record:

- **Data version** — which exact `dota-data` DATASET build (see
  `dota-data`'s pipeline — FEATURES version, filters applied, temporal
  split cutoff) was used. Reference it by an explicit version identifier,
  not "the current dataset" or a mutable path.
- **Code version** — the exact commit/revision of training code used.
- **Hyperparameters** — the full configuration, not just the ones that
  differ from defaults (defaults change across library versions, which
  silently breaks reproducibility if only overrides are recorded).
- **Random seed(s)** — for any stochastic step (train/validation
  splitting within temporal windows, model initialization, subsampling).
- **Library/dependency versions** — at minimum the ML library
  (LightGBM/XGBoost/scikit-learn) version, since default behaviors and
  even numerical results can shift across versions.

Store this as structured metadata alongside the model artifact itself
(not in a separate document that can drift out of sync), so loading a
model and its training record are the same operation.

## What "equivalent" means

Bit-for-bit identical output isn't always realistic (some libraries have
platform-dependent floating-point nondeterminism even with a fixed seed).
"Equivalent" means: retraining from the recorded inputs produces a model
with materially the same performance characteristics (same ballpark
metrics on the same held-out evaluation) and the same qualitative
behavior on a small set of fixed sanity-check inputs. If a rerun produces
a meaningfully different model from supposedly identical inputs, that's a
reproducibility bug worth chasing down (an unrecorded source of
randomness, an unpinned dependency), not something to shrug off.

## Model versioning scheme

Every model artifact needs a version identifier that ties back to its
full training record. A reasonable scheme:

```
<model-name>-v<major>.<minor>-<training-run-id>
```

- **model-name** — what the model does (e.g. `item-value-estimator`,
  `win-probability`).
- **major.minor** — major bumps for architecture/feature-set changes
  that aren't a drop-in replacement (a downstream consumer, e.g. a
  Recommendation Engine, may need to change how it calls the model);
  minor bumps for retrains with the same architecture/feature contract
  (e.g. periodic retraining on newer data).
- **training-run-id** — a unique identifier (timestamp, hash, or run
  tracking system ID) linking to the full reproducibility record above.

**Every shipped model must be traceable from this version identifier back
to:** its training data version, code version, hyperparameters, and
evaluation results (including the baseline comparison from
`model-selection.md` and the calibration check from
`validation-and-calibration.md`). If a `RecommendationEngines` consumer
(per `dota-architecture`) references a model, it should reference it by
this version identifier, not an unversioned "latest" pointer, so a given
build of the application has a known, fixed model behavior rather than
one that can silently change underneath it.

## Practical storage

- Keep trained model artifacts (and their ONNX exports, see
  `onnx-export.md`) in a versioned artifact store or repository separate
  from frequently-changing application code, but reference them from
  application code by explicit version, not a path that gets overwritten.
- Record the reproducibility metadata as a structured file (e.g. JSON)
  co-located with or embedded alongside the model artifact — this is the
  same instinct as `dota-data`'s provenance rule applied at the model
  layer instead of the dataset layer.

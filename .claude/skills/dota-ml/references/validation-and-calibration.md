# Temporal Validation and Calibration

## Why temporal validation, specifically

`dota-data` already establishes temporal splits as the default for
dataset construction (train on data before a cutoff, test on data after).
This section covers what that means concretely for model *validation*
during development — not just the final train/test split, but how to
validate model choices (hyperparameters, feature sets, architecture)
without accidentally reintroducing the leakage a temporal split is meant
to prevent.

**Use rolling/expanding-window temporal cross-validation, not k-fold.**
Standard k-fold cross-validation shuffles data across folds, which
reintroduces exactly the temporal leakage a single temporal split
prevents (a "held-out" fold can still contain matches from the same time
period as training folds, including the meta/patch context that a random
shuffle doesn't respect). Instead:

- Use an expanding or rolling window: train on data up to time T₁,
  validate on T₁-T₂; train on data up to T₂, validate on T₂-T₃; etc.
- Never let a validation window's data precede any training window's
  data in time — this holds even during hyperparameter search, not just
  for the final held-out test.
- If patch boundaries are meaningful for the task, consider aligning
  validation windows to patches rather than arbitrary calendar cutoffs,
  since a patch boundary is often where the underlying data distribution
  actually shifts.

**The final test set stays untouched until the very end.** Use the
temporal cross-validation windows described above for all iteration
(feature selection, hyperparameter tuning, architecture comparison);
reserve the final, most-recent temporal slice as a true held-out test set
touched exactly once, at the end, to report final performance — not used
to pick between models along the way. Repeatedly checking performance
against the "final" test set during iteration slowly turns it into a
validation set through information leakage via your own decisions, even
without literally training on it.

## Calibration

A model that outputs "70% win probability" should be right about 70% of
the time across all the cases where it says 70% — that's calibration,
and it's a distinct property from discrimination (ranking positives above
negatives correctly). A model can have excellent ranking performance
(good AUC) while being badly calibrated (systematically over- or
under-confident), and for this project calibration matters specifically
because:

- Any probability/confidence shown to the player (directly or via the
  LLM explanation layer — see the "ML decides, LLM explains" principle
  in `SKILL.md`) needs to actually mean what it says, or it misleads the
  player about how confident the recommendation really is.
- Ranking models in particular (LightGBM/XGBoost ranking objectives) are
  optimized for relative ordering, not for producing well-calibrated
  absolute scores — if a ranking model's raw scores are ever surfaced or
  interpreted as probabilities, they need explicit calibration, not just
  a rescale.

**How to check calibration:** plot or compute a calibration curve
(predicted probability bucket vs. observed frequency in that bucket) on
the held-out temporal validation data — not training data, since a model
is trivially "calibrated" on data it was fit to. A well-calibrated model
tracks the diagonal; systematic deviation (e.g. everything in the 80%
bucket actually resolves 60% of the time) signals a needed fix.

**How to fix miscalibration:** Platt scaling or isotonic regression fit
on a held-out calibration set (not the same data used for the calibration
*check* above — use a third split, or nested cross-validation, to avoid
overfitting the calibration correction itself) are standard, simple fixes
that work well with tree-based models and are cheap to apply. Prefer
these over architecture changes as the first response to a calibration
problem — miscalibration is usually a post-hoc-fixable property, not
evidence the underlying model is wrong.

## Metrics beyond accuracy

See `evaluation.md` for the full picture, but as it relates to validation
specifically: track calibration alongside whatever discrimination/ranking
metric is primary for the task (AUC, NDCG, etc.) at every temporal
validation window, not just at the end — a model that's well-calibrated
early in the temporal validation sequence but drifts miscalibrated in
later windows is signaling a meta/data-drift problem worth investigating
before shipping, not just something to patch with a final calibration fit.

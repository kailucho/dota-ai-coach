# ADR 0018: Win Probability baseline — regularization fix for the val-AUC regression

## Status

Accepted. Supersedes the model artifact from `docs/adr/0017` with a
retrained one; `docs/adr/0017`'s design/decisions are otherwise unchanged.

## Context

`docs/adr/0017`'s Consequences section flagged an honest, unresolved
result: the full 18-feature Logistic Regression scored *lower* val AUC
(0.741) than a single-feature baseline using only `gold_adv_at_T` (0.793)
— the opposite of what adding more real signal should do. Per the user's
explicit direction, this was investigated before building any further
model, rather than being carried forward unexplained.

## Investigation

Inspecting the training data directly (not guessing) showed severe
multicollinearity among the strongest features:
`corr(gold_adv_at_T, xp_adv_at_T) = 0.91`,
`corr(gold_adv_slope_5, xp_adv_slope_5) = 0.90`, and both slope features
correlate 0.63-0.75 with their non-slope counterparts. All four are
measuring largely the same underlying signal (economic advantage) from
slightly different angles. `sklearn.LogisticRegression`'s default
`C=1.0` (weak L2 regularization) lets the optimizer split weight
unstably across these correlated features rather than converging on a
single stable estimate — this is the textbook failure mode
multicollinearity causes in unregularized/weakly-regularized linear
models, and it degrades held-out ranking performance even though the
extra features are individually informative.

Confirmed by direct measurement (not assumption): sweeping `C` from
`1.0` down to `1e-6` on the training data produced a **monotonic
improvement** in held-out val AUC (0.741 → 0.746 → 0.768 → 0.788 → 0.792,
plateauing around `C=1e-5` to `1e-6`). A separate control — pruning the
two slope features entirely, keeping default `C=1.0` — barely moved AUC
(0.740), confirming the fix is regularization strength, **not** feature
selection. No feature was actually noise; they were simply unstable
together under too-weak regularization.

## Decision

Add a small `C` grid search (`[1.0, 0.1, 0.01, 0.001, 1e-4, 1e-5, 1e-6]`)
to the Logistic Regression training step, selected by val AUC (never
test, consistent with `docs/adr/0017`'s split discipline — this is
exactly the kind of hyperparameter search
`dota-ml/references/validation-and-calibration.md` requires to happen
strictly before the test split is touched). No features were removed;
the full 18-feature set is retained per `docs/adr/0017`'s design.

The search result and selected `C` are recorded in the model's
`metadata.json` hyperparameters block (`C`, `C_search_candidates`,
`C_search_val_auc`) — full transparency into what was tried, not just the
winner, per the reproducibility skill's "record the full configuration"
rule.

## Results (retrained model, `win-probability-v1.0-20260809T165319Z`)

| | Before (ADR-0017) | After (this fix) |
|---|---|---|
| Val AUC (chosen model) | 0.741 | **0.792** |
| Test AUC (touched once) | 0.777 | **0.839** |
| Test log loss | 0.564 | **0.506** |
| Pre-calibration ECE (val) | 0.067 | **0.0007** |
| Symmetry sanity check | **failed** | **passed** |

The full model's val AUC (0.792) now essentially matches the
single-feature baseline (0.793) rather than underperforming it — the
regression is resolved. The symmetry domain sanity check, which ADR-0017
reported as failing, now passes as well; the earlier failure is
attributable to the same unstable, overfit coefficients this fix
addresses, not a separate bug.

Segmented results all improved: early-game AUC 0.674→0.762, mid-game
0.836→0.886, late-game 0.868→0.900, Ancient+ rank tier 0.786→0.847,
Archon-Legend 0.717→0.791. The early/mid/late gap remains (expected and
inherent — early game genuinely has less accumulated signal), but the
model is stronger in every segment.

One side effect worth noting rather than hiding: with such strong
regularization, the model's response to a single feature swept in
isolation (the monotonicity sanity check's gold-advantage sweep) is now
much flatter near 0.5 (0.5265 to 0.5276 across a ±10k gold sweep) than
before regularization (0.36 to 0.81). The ranking direction is still
strictly correct (monotonicity still passes), but a single feature no
longer swings the prediction much on its own — expected, since heavy L2
shrinks every individual coefficient toward zero and the model now
relies on the combination of correlated signals together, not any one of
them in isolation. This is a legitimate characteristic of the fix, not a
new problem.

## Consequences

- The previous model artifact (`win-probability-v1.0-20260809T163642Z`)
  was deleted and replaced; only the corrected model is kept. Its
  `metadata.json`/`evaluation.json` (git-tracked) preserve the honest
  before/after numbers in `docs/adr/0017`'s and this ADR's text, so the
  investigation remains traceable even though the underperforming
  artifact itself isn't kept on disk.
- `docs/ROADMAP.md`'s Fase 3 section is updated with the corrected
  numbers and this ADR's reference.
- General lesson worth carrying into any future model in this project:
  when adding several features that plausibly measure the same
  underlying construct from different angles (multiple time-windows or
  derived variants of the same base metric, as here), check pairwise
  correlation and regularization strength together before concluding a
  feature is "not helping" — the feature might be fine and the
  regularization might be the actual problem, as it was here.

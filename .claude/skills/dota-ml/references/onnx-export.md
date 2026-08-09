# ONNX Export

## When it applies

Export to ONNX for any model that will run **live**, inside the desktop
application, consumed via `dota-architecture`'s `Ml.Inference` layer
(ONNX Runtime). This is what makes the model usable in the shipped
Windows app without requiring the training stack (LightGBM/XGBoost/
scikit-learn/Python, if that's the training toolchain) to be present at
runtime — see `dota-architecture`'s "ML must not depend on WPF/WinUI"
rule and its inverse implication: the live app also shouldn't need the
full training/Python stack just to run inference.

**Not every model needs ONNX export.** Models used purely offline —
research/analysis models, models that only inform an understanding of the
game rather than feeding a live recommendation, exploratory work before a
baseline is settled — don't need export. Export is specifically for the
handoff from `Training` to `Ml.Inference` in the live pipeline. Exporting
prematurely (before a model is validated, calibrated, and versioned per
the rest of this skill) just creates stale artifacts to clean up later.

## What changes between training-time and inference-time behavior

Export is not just a format conversion — verify these don't silently
change behavior:

- **Preprocessing must travel with the model, or be re-implemented
  identically on the inference side.** If training-time feature
  engineering (scaling, encoding, the Feature Engine's transformations
  per `dota-architecture`) isn't part of the exported graph, the
  `Ml.Inference` consumer must apply exactly equivalent preprocessing —
  any drift here (a different scaling constant, a different encoding of
  a categorical feature) silently produces wrong predictions without any
  error. Prefer including preprocessing in the exported ONNX graph itself
  where the library supports it (e.g. via `sklearn-onnx`'s pipeline
  support, or ONNX's built-in preprocessing ops), since that removes the
  drift risk structurally rather than relying on two implementations
  staying in sync.
- **Numerical precision** — ONNX Runtime may use different numeric
  precision/execution providers than the training library; for most
  tabular models (LightGBM/XGBoost/Logistic Regression) this is rarely a
  practical issue, but verify rather than assume, especially for any
  model where small numeric differences could flip a ranking near a
  decision boundary.
- **Missing-value/default handling** — confirm the exported model handles
  missing/absent features (e.g. a feature genuinely unavailable at a
  given game timestamp — see `leakage-and-feature-availability.md`) the
  same way the training-time model did, not silently substituting a
  different default.

## Export validation checklist

Before trusting an exported model in the live path:

- [ ] Run a fixed set of test inputs (ideally including edge cases —
  missing features, boundary values) through both the original
  (pre-export) model and the exported ONNX model; confirm outputs match
  within an acceptable numerical tolerance.
- [ ] Confirm the ONNX model's expected input schema (feature names,
  order, types) is documented and matches exactly what `Ml.Inference`/the
  Feature Engine will actually provide at inference time — a silent
  feature-order mismatch is a classic, hard-to-detect export bug.
- [ ] Re-run the calibration check (`validation-and-calibration.md`)
  against the exported model's outputs specifically, not just the
  pre-export model — export bugs can subtly shift output distributions.
- [ ] Confirm the exported model is tagged with the same version
  identifier scheme as its pre-export counterpart
  (`reproducibility-and-versioning.md`), so the ONNX artifact traces back
  to the same training record.
- [ ] Confirm inference latency on representative hardware is acceptable
  for a live, in-game overlay use case — a model that's accurate but too
  slow for live inference isn't actually usable in this product's context,
  and this is a materially different constraint than most ML deployment
  contexts.

## Where export artifacts live

Per `dota-architecture`'s solution structure, `.onnx` files are a build
artifact of `Training` (or a promotion step after `Training` produces a
validated model), consumed by `MlInference`. Keep them versioned and
traceable per `reproducibility-and-versioning.md` — an ONNX file with no
recorded provenance is exactly as risky as any other unversioned model
artifact, format aside.

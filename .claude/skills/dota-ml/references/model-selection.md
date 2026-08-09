# Model Selection: Baseline First, Earn Complexity

## Why baseline-first is a rule, not a suggestion

Without a baseline, a "successful" model's metric is meaningless — you
can't tell whether 72% accuracy is impressive or whether a two-line
Logistic Regression would have hit 71%. Baseline-first isn't about being
conservative for its own sake; it's the only way to know whether the
complexity you're about to add is actually earning its cost (training
time, inference cost on the player's machine, interpretability,
ONNX-exportability, maintenance burden).

## The preferred progression

### 1. Logistic Regression (or plain linear/regularized regression for
regression tasks)

Build this first, always, even if you're confident it won't be the final
model. It's fast to train, trivial to reproduce, easy to inspect
(coefficients map directly to feature effects, which you can sanity-check
against `dota-domain` expectations), and gives you the number every later
model needs to beat.

If Logistic Regression already performs close to what's needed, that's a
genuinely good outcome — ship it. Simpler models are easier to keep
correct, faster at live inference, and much easier to debug when
something looks wrong in production.

### 2. LightGBM / XGBoost

The natural next step once the linear baseline is beaten by a meaningful
margin. Gradient-boosted trees handle nonlinearity and feature
interactions well, remain reasonably interpretable via feature importance
and SHAP-style analysis, train fast even on modest hardware, and export
to ONNX cleanly — a strong fit for this project's local-first, live-
inference constraints (see `dota-architecture`).

Use this as the default "real" model for most tabular prediction tasks
(win probability, item value estimation, etc.) unless there's a specific
reason not to.

### 3. Ranking models

If the actual task is "rank several candidates" (e.g. rank candidate
items by value right now) rather than "predict one number," use a model
built for ranking (e.g. LambdaMART-style objectives, available in
LightGBM/XGBoost's ranking modes) instead of forcing a
classification/regression model into a ranking role by scoring
candidates independently and sorting. A proper ranking objective learns
relative ordering directly, which is usually both more accurate and more
aligned with how the output will actually be used (a top-N list, not a
calibrated absolute score) — though see `references/validation-and-calibration.md`
for why calibration still matters if the score is ever shown to the user
as a probability/confidence.

### 4. Bayesian shrinkage

For narrow slices of data — a specific hero+item combination, a specific
matchup, anything where the raw sample size is small — a raw empirical
estimate (e.g. "this item won 8/10 games with this hero") is noisy and
overconfident. Shrink it toward a reasonable prior (e.g. the hero's
overall rate, or a role-level rate) rather than trusting the raw number
or excluding the slice entirely. This is often the highest-value,
lowest-complexity fix available for sparse-data problems in this domain,
and should be considered before reaching for a more complex model to
"learn" what shrinkage would fix more simply and more robustly.

## When a more complex architecture (Transformers, GNNs, etc.) might be justified

Complex architectures are not banned outright — they're gated. Before
introducing one, be able to answer concretely:

- **What signal does the simpler baseline structurally miss?** E.g. "the
  baseline treats each ability independently, but ability *sequencing*
  within a fight matters and a sequence-aware model could capture that" is
  a concrete claim. "It might just do better" is not.
- **Has this been tested, not just assumed?** Build the complex model,
  compare against the established baseline on the same temporal
  validation split (`references/validation-and-calibration.md`), and
  confirm the improvement is real and not just overfitting to validation
  noise.
- **Is the improvement worth the cost?** A complex model that beats
  LightGBM by a small margin but is 50x slower to run live, much harder
  to export to ONNX cleanly, and much harder to debug when a player
  reports a weird recommendation, is often a net loss for this specific
  product even with a better raw metric. Local-first, live-inference,
  desktop-app constraints (see `dota-architecture`) matter more here than
  in a typical ML project — weigh them explicitly, don't default to
  "state of the art" as the goal.
- **Does the task actually need it?** Most of this project's prediction
  tasks are tabular, feature-engineered problems (win probability from
  match state, item value from features) where tree-based models
  routinely match or beat deep architectures. Reach for
  Transformers/GNNs when the data has structure those architectures
  specifically exploit (e.g. genuine sequence/graph structure that
  feature engineering can't reasonably flatten), not as a default
  upgrade path.

If a complex architecture is genuinely justified, document the baseline
comparison (numbers, not just a description) as part of the model's
versioning record (`references/reproducibility-and-versioning.md`) so the
justification is auditable later, not just remembered.

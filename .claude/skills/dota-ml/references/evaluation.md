# Evaluation Guidance

## Start from the task, not the metric

Pick the metric family based on what the model's output actually is and
how it'll be used — a common source of misleading "good" results is
evaluating a model on a metric that doesn't match its actual usage.

- **Binary/multi-class classification** (e.g. "will this team win") —
  AUC-ROC for ranking quality, log loss for probability quality, plus a
  calibration check (`validation-and-calibration.md`) if the probability
  itself will ever be shown or reasoned about downstream (including by
  the LLM explanation layer — see "ML decides, LLM explains" in
  `SKILL.md`). Accuracy alone is a weak metric for most of this project's
  tasks, especially on imbalanced outcomes — don't rely on it as the
  primary metric.
- **Ranking** (e.g. "rank these candidate items by value right now") —
  NDCG, MRR, or a similar ranking-appropriate metric evaluated on the
  actual candidate sets the model will see at inference (not on an
  artificially easy or artificially large candidate pool that doesn't
  match production usage).
- **Regression** (e.g. estimating a continuous value like expected net
  worth at a timestamp) — RMSE/MAE, plus a residual analysis by segment
  (see below) rather than trusting a single aggregate number.
- **Small-sample/shrinkage estimates** — evaluate the shrinkage itself:
  does the shrunk estimate for sparse slices track held-out performance
  better than the raw empirical estimate would have? This is a direct,
  checkable claim, not just an assumption that shrinkage helps.

## Segment the evaluation, don't just report an aggregate

An aggregate metric can hide the fact that a model works well overall but
badly for a strategically important subgroup. Break out evaluation by
dimensions that matter for this project specifically:

- **By game phase / timestamp bucket** (early/mid/late game) — a model
  might be strong late-game (more data accumulated) and weak early-game
  (sparse signal), which matters a lot for a live coaching tool that
  needs to be useful throughout the match, not just at the end.
- **By skill bracket**, if the model is meant to generalize across
  brackets — per `dota-domain`, strategy that's correct at one bracket
  isn't automatically correct at another, and a model trained mostly on
  one bracket's data may not transfer.
- **By patch**, especially near a validation window's patch boundary
  (see `validation-and-calibration.md`) — a sudden performance drop right
  after a patch change is a meaningful signal about how well the model
  handles meta shifts, not noise to average away.
- **By hero/role**, where sample size allows — per-hero performance
  variance is expected, but a systematic weakness for a whole role (e.g.
  consistently poor for position 5) is worth investigating rather than
  accepting inside an acceptable-looking aggregate.

## Sanity-check against domain knowledge

A model's feature importances, learned coefficients, or qualitative
behavior on constructed test cases should make basic strategic sense per
`dota-domain`. This isn't a substitute for quantitative evaluation, but
it's a cheap, high-value check:

- Construct a few hand-built input scenarios with an obvious expected
  direction (e.g. "more gold advantage at the same timestamp should not
  decrease predicted win probability, all else equal") and confirm the
  model agrees.
- If a feature the model weights heavily doesn't have an obvious
  strategic story, that's worth investigating — it might be a genuinely
  interesting finding, or it might be a leakage/data-quality artifact
  (see `leakage-and-feature-availability.md`) surfacing as an
  implausibly strong feature.

## Where "ML decides, LLM explains" affects evaluation

Because the LLM layer explains rather than decides, evaluating the LLM's
output quality (does the explanation read well, is it clear) is a
separate concern from evaluating whether the underlying model's decision
was correct — don't let a fluent, well-written explanation of a bad model
output count as evidence the model is good. Evaluate the model's
structured output (per the metrics above) independently of how well any
downstream explanation reads, and treat a mismatch between a
high-quality-sounding explanation and a poor underlying decision as a
red flag specifically about the explanation layer papering over model
quality, not as evidence the system overall is working.

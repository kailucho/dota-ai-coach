# FairPlay Tests, ML Leakage Tests, Temporal Validation Tests

These three categories are grouped together because they all answer a
variant of the same question: **is this system only using information it
is legitimately allowed to have, at the time it's allowed to have it?**
Full depth lives in `dota-fairplay` and `dota-ml`; this file explains how
each fits into the overall test suite and Definition of Done.

## FairPlay tests

**What they prove:** a live feature's data source is classified correctly
(one of the six categories in `dota-fairplay`), no `FORBIDDEN` or
`SPECTATOR_ONLY` data is reachable from any live path, and
`POSTGAME_ONLY` data never reaches a live recommendation.

**Required for:** any change matching `dota-fairplay`'s trigger list — a
new live-game data source, a new `MatchState` field, a new CV feature, a
new model feature, anything touching opponent information or replay
data, or any modification to live recommendations. See `dota-fairplay`'s
`testing-fairplay.md` for concrete test shapes (structural
unreachability tests preferred over runtime checks where the type system
allows it).

**Why this can't be skipped or deferred:** an undetected FairPlay
violation isn't a quality bug, it's the product silently becoming
something that functions like a cheat. This is the one test category in
this skill explicitly called out as a hard gate in the Definition of
Done regardless of what else a change touches (`definition-of-done.md`).

## ML leakage tests

**What they prove:** every feature used by a model is actually computable
using only data available as of its claimed inference timestamp — not
just generally-legitimate data, but legitimate data *as of that specific
moment in the match*. See `dota-ml`'s `leakage-and-feature-availability.md`
for the concrete leakage patterns this guards against (full-match
aggregates leaking into mid-game features, latest-patch reference data
leaking into historical training examples, etc.) and the specific test
shapes (timestamp-bound feature computation tests, FairPlay
classification tests reused at the ML feature layer, patch-pinning
tests).

**Required for:** any change to a live model's feature set, or any new
feature being added to training data destined for a live model.

## Temporal validation tests

**What they prove:** a model's evaluation actually used a correctly
time-ordered validation methodology — rolling/expanding-window splits,
not a shuffled k-fold that silently reintroduces the same kind of
leakage a live-timestamp check guards against, just at the dataset-split
level instead of the feature level. See `dota-ml`'s
`validation-and-calibration.md`.

**Required for:** any model training run whose results will inform a
shipped model — this is part of the pre-ship checklist in `dota-ml`'s
`ml-checklists.md`, not just a nice-to-have during exploration.

## How these feed the Definition of Done

- Any **live-data change** (per `dota-fairplay`'s trigger list) requires
  FairPlay tests passing — see `SKILL.md`'s Definition of Done summary
  and `definition-of-done.md`'s live-data checklist.
- Any **ML/model change** feeding a live path additionally requires
  leakage tests and (for anything informing a shipped model) temporal
  validation — see `dota-ml`'s own pre-training/pre-ship checklists,
  which this skill's Definition of Done incorporates by reference rather
  than duplicating.

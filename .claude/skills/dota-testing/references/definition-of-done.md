# Definition of Done

A change is done when it satisfies the checklist for its change type(s)
below. Most changes span more than one type (e.g. a new live GSI field is
both a ".NET change" and a "live-data change") — apply every checklist
that matches, not just the first one that seems to fit.

## Baseline (applies to every change, regardless of type)

- [ ] The change does what it was intended to do, verified by at least
  one test that would fail without the change.
- [ ] If this change fixes a bug, a regression test exists per
  `regression-testing.md`, or the exception is explicitly stated and
  justified.
- [ ] Existing tests still pass — no unrelated test broken by this
  change without a deliberate, understood reason.
- [ ] The change respects `dota-architecture`'s layering — no new
  dependency edge that violates the dependency rules (see
  `dota-architecture`'s new-module checklist if this change adds a new
  project/module).

## .NET changes

Required, no exceptions:

- [ ] `dotnet build` succeeds.
- [ ] `dotnet test` passes, including any new tests added for this
  change.

Additionally, based on what the change touches:

- [ ] **Touches GSI parsing/normalization** → GSI fixture test added/
  updated (`gsi-testing.md`, `dota-gsi`'s `fixtures-and-testing.md`).
- [ ] **Touches GSI merge/diffing/staleness logic** → GSI replay
  simulator (sequence) test added/updated.
- [ ] **Touches CV extraction** → CV test added/updated against known
  captures, including an "unavailable" case (`cv-and-performance-tests.md`).
- [ ] **Touches a live-path performance-sensitive component** (GSI
  ingestion, CV extraction, inference, orchestration) → performance
  benchmark checked, not just functional correctness
  (`cv-and-performance-tests.md`).
- [ ] **Touches multiple pipeline stages' interaction** in a way not
  covered by existing end-to-end scenarios → end-to-end test added/
  updated (`end-to-end-tests.md`).

## Python changes (data/ML tooling)

Required, no exceptions:

- [ ] `ruff` passes (linting/formatting).
- [ ] `pyright` passes (type checking).
- [ ] `pytest` passes, including any new tests added for this change.

Additionally, based on what the change touches:

- [ ] **Touches a live model's feature set** → ML leakage tests added/
  updated (`fairplay-and-ml-tests.md`, `dota-ml`'s
  `leakage-and-feature-availability.md`).
- [ ] **Touches training/evaluation methodology for a model informing a
  shipped model** → temporal validation confirmed correct
  (`fairplay-and-ml-tests.md`, `dota-ml`'s `validation-and-calibration.md`),
  and the model has been through `dota-ml`'s pre-training/pre-ship
  checklists as appropriate to how far along it is.
- [ ] **Touches the `dota-data` pipeline** (RAW→BRONZE→NORMALIZED→
  FEATURES→DATASET) → relevant `dota-data` quality checklist run
  (pre-ingestion/pre-training/pre-release, per `dota-data`'s
  `data-quality-checklists.md`).

## Live-data changes

Applies to any change matching `dota-fairplay`'s trigger list: a new
live-game data source, a new `MatchState` field, a new CV feature, a new
model feature, anything working with opponent information, anything
working with replay data, or a modification to live recommendations.

Required, no exceptions, **in addition to** whichever of the .NET/Python
checklists above also apply:

- [ ] The feature's `source`/`availability`/`visibility`/
  `fairPlayClassification`/`justification` are documented per
  `dota-fairplay`'s `feature-documentation-template.md`.
- [ ] A FairPlay test exists asserting the classification holds
  structurally (not just documented) — see `dota-fairplay`'s
  `testing-fairplay.md` and this skill's `fairplay-and-ml-tests.md`.
- [ ] No `FORBIDDEN` or `SPECTATOR_ONLY` data is reachable from the live
  path as a result of this change.
- [ ] If the change touches a live model, ML leakage tests
  (`fairplay-and-ml-tests.md`) confirm every feature is timestamp-bound
  correctly, per `dota-ml`'s critical rule.

**This checklist is a hard gate.** A live-data change that passes
`dotnet test`/`pytest` but hasn't been through FairPlay validation is not
done — clean, well-tested code that leaks illegitimate information is
still a serious defect, arguably a more serious one than a functional bug,
because of what it does to the product's basic trustworthiness.

## Quick reference: which checklist(s) apply

| Change touches... | Baseline | .NET | Python | Live-data |
|---|---|---|---|---|
| C#/WPF/WinUI code | ✓ | ✓ | | |
| Python data/ML tooling | ✓ | | ✓ | |
| New GSI field or CV feature | ✓ | ✓ (if .NET) | ✓ (if Python) | ✓ |
| New ML feature for a live model | ✓ | | ✓ | ✓ |
| Opponent info or replay data | ✓ | (as applicable) | (as applicable) | ✓ |
| Pure offline/analysis-only ML work | ✓ | | ✓ | (not required unless it later feeds a live model) |
| Domain/strategy logic only (no new data source) | ✓ | ✓ (if .NET) | | |

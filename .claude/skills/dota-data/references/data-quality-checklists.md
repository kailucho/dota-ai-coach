# Data Quality Checklists

Three checklists, used at different points in the pipeline's lifecycle.
Run the relevant one before trusting the result, not after something has
already gone wrong downstream.

## Pre-ingestion checklist (before adding/changing a data source)

Run this before wiring up a new API, endpoint, or bulk-fetch job.

- [ ] **Auth/rate limits confirmed against current docs**, not assumed
  from memory — see `apis-and-identifiers.md`'s ground-truth warning.
- [ ] **RAW capture plan defined**: what gets stored, in what format,
  with what request/fetch metadata (source, timestamp, params).
- [ ] **Private/missing-profile response shape identified** for this
  source, so BRONZE parsing can detect and tag it rather than silently
  mis-parsing it (rule 6).
- [ ] **Steam ID form documented** for this source (Steam32 vs Steam64)
  if the source returns player identifiers, and a canonicalization plan
  exists (`apis-and-identifiers.md`).
- [ ] **Patch/timestamp availability confirmed**: does this source
  provide patch directly, or does NORMALIZED need to derive it? Is a
  current patch-date lookup table available and up to date?
- [ ] **Cost of replay parsing (if applicable) budgeted** — parsing is
  expensive; confirm the ingestion plan doesn't parse indiscriminately.

## Pre-training checklist (before building/using a DATASET)

Run this before kicking off a training run against a dataset, whether
newly built or previously built and being reused.

- [ ] **Turbo/Ranked (or other mode) selection is explicit** — confirm
  the dataset's mode filter matches what this specific training task
  needs, not just whatever the last build happened to include (rule 1).
- [ ] **Abandoned/incomplete game handling is documented** for this
  dataset build and appropriate for the task (rule 2).
- [ ] **Every record carries patch and timestamp** — spot-check a sample
  for nulls/missing values before trusting the full dataset (rule 3).
- [ ] **No duplicate `match_id`s** — run an explicit dedup check on the
  DATASET, don't just assume NORMALIZED's dedup step caught everything
  (rule 4).
- [ ] **Provenance is intact** — spot-check that a few DATASET records can
  actually be traced back to their RAW origin; a broken provenance chain
  here means debugging a bad model later will be much harder (rule 5).
- [ ] **Split is temporal** (or the choice to use a different split is
  explicitly justified for this task) — confirm the actual date cutoff
  used, not just that a split function was called (rule 7).
- [ ] **Leakage check performed** — near-duplicate matches,
  over-concentrated players, and any feature with potential future-
  information leakage have been checked for this specific dataset build,
  not just assumed clean because a previous build was checked (rule 8).

## Pre-release checklist (before shipping a dataset or a model trained from it)

Run this before a dataset or trained model is considered final/shippable
— e.g. before it's used to produce a live recommendation model artifact.

- [ ] **Dataset build is fully version-documented**: FEATURES version
  used, filters applied (mode, abandon handling, patch range), temporal
  split cutoff, and total record counts per split.
- [ ] **All eight data-quality rules re-confirmed** against this specific
  build, not inherited from a prior build's checklist run — pipeline code
  changes over time and a rule that held for an earlier build isn't
  guaranteed to hold for a new one without re-checking.
- [ ] **FairPlay classification confirmed** for any data feeding a live
  model derived from this dataset — see the `dota-fairplay` skill; a
  dataset itself can be legitimately built from `POSTGAME_ONLY`/
  historical data, but the *model trained from it* must not leak that
  classification into a live feature in a way that violates FairPlay's
  rules (see `dota-fairplay`'s worked example on this exact failure mode).
- [ ] **Reproducibility confirmed**: given the same version-documented
  build parameters, could this dataset be rebuilt from RAW/BRONZE and
  produce an equivalent result? If RAW has since been pruned/expired,
  document that explicitly rather than silently losing reproducibility.

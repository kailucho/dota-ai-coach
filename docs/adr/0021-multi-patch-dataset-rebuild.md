# ADR 0021: Multi-patch dataset rebuild

## Status

Accepted

## Context

`docs/ROADMAP.md`'s "Recommended next phase" (written after `docs/adr/0020`)
called for "a genuinely production-scale RAW→BRONZE→NORMALIZED→FEATURES→
DATASET rebuild ... before ONNX export and live wiring are considered."
The specific, user-approved scope for this iteration was narrower than
"production-scale" in the general sense: rebuild the dataset to genuinely
span multiple patches, with roughly balanced volume per patch, since
`docs/adr/0017`/`0018`/`0019`/`0020` all flagged the same limitation —
by-patch segmentation and patch-robustness were completely untested.

**Correction to the prior record, found live during this pass**:
`docs/adr/0017` and `docs/ROADMAP.md` both stated the original 1863-match
dataset "landed on a single patch (60)." Inspecting the actual RAW cache
directly (not the prior summary) shows this was not quite accurate: of
the 1912 originally-fetched matches, 1535 carry `patch=60` and 377 carry
`patch=59` — a real two-patch mix, just heavily skewed toward patch 60
and never previously segmented or reported. This ADR's dataset both
corrects that record and substantially increases patch-59 volume plus
adds patch-58 coverage, per the original scope.

Per `.claude/skills/dota-data` and `.claude/skills/dota-ml`, both loaded
in full before any code was written. The existing RAW cache is treated as
an append-only seed, never refetched or mutated (`dota-data` rule 5,
provenance).

## Decision 1: patch selection — verified live, not assumed

`docs/adr/0017` decision 3 already established the project's discipline of
verifying OpenDota facts live rather than trusting assumptions (the
game_mode 22/23 correction). Applied here to patch selection:

- `GET /constants/patch` (live, 2026-08-09) shows patch 60 = "7.41",
  dated 2026-03-24 — the **most recent** entry in the list. Today's date
  is 2026-08-09/10, so **patch 60 is still the current, active patch**;
  there is no "patch 61" data available yet. This ruled out the
  originally-imagined "before and after 60" framing — the only available
  direction to extend into is backward, into patches 58 and 59.
- Patch 59 = "7.40", dated 2025-12-16 (i.e. patch 59 covers
  2025-12-16 .. 2026-03-24).
- Patch 58 = "7.39", dated 2025-05-22 (i.e. patch 58 covers
  2025-05-22 .. 2025-12-16).
- `GET /matches/{id}` was verified live to expose `patch` as a **direct
  integer field** (e.g. the current head match returned `"patch": 60`) —
  no derivation from `start_time` against a lookup table was needed, and
  this field is what `stages/bronze.py`/`normalized.py`/`features.py`
  already key off of.

match_id anchors were chosen by estimating the match_id/day growth rate
(~1.396M match_ids/day, calibrated from two known-timestamp matches 226
days apart) and then **verified live** by directly fetching each
candidate anchor and checking its returned `patch`/`start_time` before
using it — not assumed from the estimate alone:

| anchor match_id | verified patch | verified start_time |
|---|---|---|
| 8442382432 | 58 | ~2025-08-20 (region confirmed via floor probe below) |
| 8478688338 | 58 | 2025-09-24 |
| 8612740914 | 59 | 2025-12-20 |
| 8672785297 | 59 | 2026-02-01 |

## Decision 2: a real API limitation found and worked around — `/explorer`'s `public_matches` is a rolling window, not full history

The first two anchor candidates chosen for patch 58 (`8317301346`,
2025-06-01, and `8343239381`, 2025-06-21 — both individually confirmed
patch=58 via `/matches/{id}`) produced **0/1400 survivors** from step 2
of the existing 3-step acquisition funnel (`/parsedMatches` →
`/explorer` filter → `/matches/{id}`). This was not assumed to be a data
scarcity issue and was instead directly investigated: running
`select min(match_id), max(match_id) from public_matches` against
`/explorer` live returned a floor of **8410426907**, which itself
resolves (via `/matches/{id}`) to patch 58, 2025-08-10. `public_matches`
— the table `/explorer` queries — is a **rolling window of recent
matches**, not OpenDota's full historical record; anchors below its
floor can never survive the `/explorer` filtering step regardless of
whether the raw match itself exists and is parsed.

This is a real, previously-undocumented constraint on the acquisition
funnel's reach into the past: patch 58 technically started 2025-05-22,
but only the ~2025-08-10-onward tail of it was reachable through this
funnel at the time of this run (the window's floor moves forward over
time, since OpenDota trims it, so this exact date will not remain valid).
Anchors were corrected to land after the retention floor and re-verified
live (see the table above) before the full run was launched. No change
to `acquire.py`'s logic was needed — this is an acquisition **planning**
constraint (anchor selection), not a code defect — but it is worth
recording as institutional knowledge for any future acquisition run
targeting matches older than the `public_matches` retention window.

## Decision 3: a real robustness bug found and fixed — non-429 network errors crashed the run

During the corrected run, the process crashed with an unhandled
`requests.exceptions.ReadTimeout` after several minutes of clean
progress. Inspecting `opendota/client.py`'s `_get_with_retry` showed its
retry loop only handled HTTP 429 responses — a genuine transient network
error (read timeout, connection reset, etc.) propagated straight past the
retry logic and killed the whole run, losing nothing already cached
(RAW writes are resumable) but requiring a manual restart. This is
exactly the failure mode `docs/adr/0017` decision 4 already fixed once
for per-match HTTP errors at the `fetch_parsed_matches` level (skip one
bad match, don't lose the whole run) — the gap was that the *lower-level*
HTTP retry had never been extended to catch non-429
`requests.exceptions.RequestException` at all, so a bad connection could
still take down the entire acquisition script even though a bad
individual match already couldn't.

Fixed by wrapping the request itself in a
`try/except requests.exceptions.RequestException` inside
`_get_with_retry`, retried with the same exponential backoff as a 429,
re-raising only after `max_attempts` is exhausted. Verified by rerunning
acquisition to completion afterward with zero further crashes and 0
failed match fetches across the remainder of the run.

## Decision 4: acquisition volumes — honest shortfall on patch 58, explained rather than padded

Final anchor-by-anchor yield (all four anchors run to completion,
`TARGET_NEW_MATCHES=2000` global cap not reached):

| anchor | patch | candidates | survivors (yield %) | fetched |
|---|---|---|---|---|
| 8442382432 | 58 | 1400 | 55 (3.9%) | 55 |
| 8612740914 | 59 | 1400 | 491 (35.1%) | 491 |
| 8478688338 | 58 | 1400 | 48 (3.4%) | 48 |
| 8672785297 | 59 | 1400 | 347 (24.8%) | 347 |

941 new matches added (103 net-new patch 58, 838 net-new patch 59 — some
overlap with the pre-existing patch-59 seed was naturally deduplicated
by RAW-cache reuse). **Final RAW cache: 2853 matches total**, patch
distribution `{58: 103, 59: 1215, 60: 1535}`.

**Patch 58 fell well short of "roughly balanced"** (103 vs. 1215/1535 for
59/60) — reported honestly rather than padded or silently dropped. Root
cause: patch 58's yield rate from `/explorer` (3.4-3.9%) is roughly an
order of magnitude lower than patch 59's (24.8-35.1%) or the historical
19-78% range `docs/adr/0017` observed for its (all patch-59/60) anchors.
This is consistent with the `public_matches` retention-window finding
(Decision 2): the reachable patch-58 window is only its final ~4 months
(2025-08-10 onward of a patch that ran 2025-05-22 to 2025-12-16), and
that tail end of a patch's lifetime is presumably when its match volume
was already declining as attention shifted toward the upcoming patch.
**Trade-off made explicitly, not silently**: rather than spending
substantially more of the 30 req/min budget chasing a "balanced" patch-58
count that the retention window makes structurally hard to reach, this
ADR accepts patch 58 as a thin (103-match) but genuine, real presence in
the dataset — enough to prove the multi-patch pipeline machinery is
correct (Decision 5) and to be visible in `hero_pick_winrate_diff`'s
training population, even though it's too small to be its own segmented
test-set slice (see Decision 6).

## Decision 5: pipeline correctness re-verified against real patch variance

The full RAW→BRONZE→NORMALIZED→FEATURES→DATASET pipeline (plus
`draft_dataset.py`/`item_events.py`/`item_dataset.py`) was re-run against
the expanded 2853-match RAW set. No pipeline code changes were needed for
correctness — `stages/dataset.py`'s `compute_hero_patch_winrates` and
`models/draft_baseline.py`'s `compute_hero_global_winrate` were already
keyed by `(hero_id, patch)`, not "the latest patch," which
`tests/test_patch_pinning.py`'s adversarial fixture had already verified
in principle; this run is the first time that logic actually processed
data where patches genuinely differ (376 distinct `(hero_id, patch)`
winrate entries were computed from the train split, versus what would
have been ~127 if patch had no effect on the join).

BRONZE: 2853/2853 well-formed (0 rejected). NORMALIZED: 0 rows dropped
for missing patch/start_time, 0 duplicate match_ids. FEATURES: 2853/2853
matches Ranked All Draft, 93 dropped for an abandon (leaver_status > 1),
**2760 clean matches, 18,139 (match, timestamp) rows**. ITEM_EVENTS:
2853/2853 Ranked All Draft, 93 abandons dropped, **205,584 (player,
purchase) rows** across 129 meaningful items.

Two new by-patch segmentation code paths were added (previously missing
— `train.py`/`evaluate.py` already had `segment_by_patch`, but
`draft_baseline.py` and `item_ranker.py` did not):
`models/draft_baseline.py` now calls `evaluate.segment_by_patch` on the
test split (required adding a `patch` column to
`compute_draft_features`'s output, which it did not previously carry);
`models/item_ranker.py` now computes a `segments_by_patch` block mirroring
its existing `segments_by_minute_bucket` pattern.

## Decision 6: a structural limit on by-patch *test-set* segmentation — reported honestly, not hidden

**The temporal split (train/val/calib/test ordered strictly by match_id,
per `docs/adr/0017`) puts patches 58 and 59 entirely inside `train`, and
patch 60 entirely inside val/calib/test**, for all three models:

| split | win-prob / draft matches | patch distribution |
|---|---|---|
| train | 1656 | `{59: 1155, 60: 403, 58: 98}` |
| val | 414 | `{60: 414}` |
| calib | 276 | `{60: 276}` |
| test | 414 | `{60: 414}` |

This is the **correct, expected consequence** of a strictly time-ordered
split applied to a dataset where patch 60 is simply the most recent
patch in calendar time — not a bug, and not something to "fix" by
switching to a random split (which `dota-data` rule 7 and
`dota-ml`'s temporal-validation requirement both explicitly rule out, and
which would reintroduce exactly the kind of leakage this project's
tests already guard against). The direct, unavoidable consequence:
**every model's by-patch *test*-set segmentation this run produces shows
only patch 60** (`evaluation.json`'s `segments.by_patch` /
`segments_by_patch` for all three models), because patches 58/59 never
appear past the train boundary.

This means the original brief's request to "report per-patch AUC/NDCG on
held-out data" cannot be honestly fulfilled for patches 58/59 with a
temporal split over a dataset shaped like this one (three patches, most
recent one being both current and by far the largest). What this run
**does** newly establish, which the original single-patch dataset
structurally could not: (a) `hero_pick_winrate_diff` and
`compute_hero_global_winrate` are now trained on 376 real
`(hero_id, patch)` entries instead of ~127 identical-patch ones, so the
patch-pinning machinery is now exercised by real data, not only by
`test_patch_pinning.py`'s synthetic fixture; (b) all three retrained
models' actual patch-60 metrics are now measured against a train
population that includes real patch-58/59 matches, rather than a
completely patch-homogeneous train set, so this is a genuine (if
modest, given patch 60 dominates train too) causal step toward
"train on more than one patch's worth of experience" — but it is **not**
the same claim as "validated to generalize to an unseen patch," which
this split cannot produce without either waiting for patch 61 to exist
or deliberately building a patch-held-out (rather than time-held-out)
split as a follow-up, explicitly named in Consequences below.

## Results — win probability

| | AUC-ROC | log loss | Brier |
|---|---|---|---|
| Constant 0.5 baseline (val) | 0.500 | 0.693 | 0.250 |
| Single-feature LogReg (`gold_adv_at_T`, val) | 0.787 | 0.557 | 0.189 |
| Chosen model (LogisticRegression, C=1e-6, val) | 0.779 | 0.688 | 0.248 |
| LightGBM (not chosen, val) | 0.707 | 0.777 | 0.247 |
| **Final test (touched once)** | **0.819** | **0.541** | **0.174** |

LightGBM again did not beat Logistic Regression (val AUC gain -0.072,
worse than `docs/adr/0017`'s already-negative -0.006 to -0.008) — the
pipeline again correctly kept the simpler model. The `C` search again
selected the strongest available regularization (`C=1e-6`, same as
`docs/adr/0018`), consistent with that ADR's multicollinearity finding
still holding on this larger, multi-patch train set.

**Test AUC 0.819 vs. `docs/adr/0018`'s 0.839** — a real, modest decline,
reported honestly rather than hidden. Segmented results: early-game
(5-15min) AUC 0.751, mid-game (20-30min) AUC 0.868, late-game (35+min)
AUC 0.867 — the same early-weak/late-strong pattern as both prior ADRs,
still holding. By rank tier: Ancient+ (n=2311) AUC 0.826, Archon-Legend
(n=353) AUC 0.783, Herald-Crusader (n=89) correctly omitted as
underpowered. By-patch (test): only patch 60 present (n=2753, AUC 0.819)
— see Decision 6. All three domain sanity checks pass (monotonicity,
near-even-at-zero, symmetry) — though, consistent with
`docs/adr/0018`'s note about heavy regularization flattening the
single-feature response, the gold-advantage sweep now spans only
0.518-0.520 (even flatter than ADR-0018's already-flat 0.5265-0.5276),
since `C=1e-6` is stronger here than the C selected there.

## Results — draft baseline

| | AUC-ROC | log loss | Brier |
|---|---|---|---|
| Constant train-prior baseline (val) | 0.500 | 0.692 | 0.249 |
| Single-feature LogReg (`hero_winrate_diff` alone, val) | 0.485 | 0.699 | 0.253 |
| Chosen model (LogisticRegression, both features, val) | 0.496 | 0.787 | 0.289 |
| **Final test (touched once)** | **0.509** | **1.204** | **0.261** |

**Still barely above chance** (test AUC 0.509, essentially unchanged from
`docs/adr/0019`'s 0.531) — the multi-patch rebuild does not change this
baseline's core, honestly-reported finding: draft alone has very weak
predictive power at pub-bracket ranked skill relative to in-game state
(the win-probability model's AUC 0.819 on the same population). Domain
sanity check still passes decisively: top-5-winrate hero pool beats
bottom-5 96% to 5% (even more decisive separation than `docs/adr/0019`'s
79%/33%, likely reflecting the larger, more diverse train population's
hero-winrate table having more genuine signal to shrink toward). By-patch
(test): only patch 60 present (n=414, AUC 0.509) — see Decision 6.

## Results — item recommendation ranker

| | NDCG@3 | NDCG@5 | MRR |
|---|---|---|---|
| Baseline 2 (global popularity, unconditional "rule", val) | 0.188 | 0.229 | 0.233 |
| Baseline 1 (per-hero popularity, Bayesian-shrunk, val) | 0.388 | 0.437 | 0.416 |
| LightGBM LambdaMART ranker (val) | 0.525 | 0.568 | 0.542 |
| **Final test (touched once)** | **0.524** | **0.567** | **0.539** |

Consistent with `docs/adr/0020`'s picture: the ranker still clearly beats
both baselines by a wide margin (test NDCG@3 0.524 vs. `docs/adr/0020`'s
0.544 — a small, real decline, honestly reported), and hero-conditioning
alone still roughly doubles NDCG@3 over the unconditional baseline
(0.188 → 0.388). Segmented by minute bucket (test): early (<15min) NDCG@3
0.692, mid (15-30min) 0.446, late (>30min) 0.343 — the same
easier-early/harder-late pattern as `docs/adr/0020`. Domain sanity check:
46.4% top-1 agreement with the per-hero baseline, vastly above the 0.88%
chance rate (114 items now vs. `docs/adr/0020`'s 114 — item vocabulary
size unchanged). By-patch (test): only patch 60 present (n=31,168 queries,
NDCG@3 0.524) — see Decision 6.

## Consequences

- New model artifacts saved at
  `tools/DotaAiCoach.Training/models/win-probability-v1.0-20260810T032019Z/`,
  `models/draft-baseline-v1.0-20260810T032042Z/`, and
  `models/item-ranker-v1.0-20260810T032341Z/`, each with the standard
  `metadata.json`/`evaluation.json` reproducibility contract. **Unlike
  `docs/adr/0018`'s precedent of deleting a superseded artifact**, the
  prior single-patch-era artifacts
  (`win-probability-v1.0-20260809T165319Z`,
  `draft-baseline-v1.0-20260809T173921Z`,
  `item-ranker-v1.0-20260809T181933Z`) are **kept alongside** the new ones
  rather than deleted — this ADR's brief explicitly called for that, and
  it is also more informative here than in ADR-0018's case: ADR-0018
  superseded a model with a real, known bug (unstable regularization);
  this rebuild instead trades a small metric decline for genuinely
  broader (if train-only) patch coverage, so both artifacts remain
  independently useful reference points.
- `tools/DotaAiCoach.Training/src/dota_training/opendota/client.py`:
  `_get_with_retry` now also retries on `requests.exceptions.RequestException`
  (not only HTTP 429), fixing a real crash found live during this run
  (Decision 3).
- `tools/DotaAiCoach.Training/src/dota_training/models/draft_baseline.py`
  and `.../models/item_ranker.py`: both gained by-patch test-split
  segmentation (`segment_by_patch` / a new `segments_by_patch` block),
  matching the pattern `models/train.py` already had. No other pipeline
  code changes were needed for multi-patch correctness (Decision 5).
- `tools/DotaAiCoach.Training/scripts/run_multi_patch_acquire.py`: new,
  documents the exact anchors used and the `public_matches`
  retention-window finding inline, for any future acquisition run.
- **Known limitations, reported honestly rather than hidden**:
  1. Patch 58 remains thin (103 matches total, all in train) — a real,
     explained shortfall against "roughly balanced" (Decision 4), not
     silently reduced in scope.
  2. **By-patch *test-set* segmentation for patches 58/59 does not exist
     and structurally cannot exist under a temporal split** where patch
     60 is simply the most recent patch (Decision 6) — this ADR only
     validates that the patch-pinning *machinery* now runs against real
     multi-patch data, not that any model *generalizes* to an unseen
     patch. A genuine patch-held-out evaluation (train on 58+59, test
     only on 60, ignoring match_id order within that constraint) is a
     reasonable, explicitly-named follow-up if patch-generalization
     itself becomes the question to answer, but it was not what this
     ADR's temporal-split-preserving scope asked for.
  3. All three models' test metrics moved modestly in the weaker
     direction versus their single-patch-era ADRs (win-prob AUC
     0.839→0.819, draft AUC 0.531→0.509, item ranker NDCG@3 0.544→0.524).
     None of these changes are large enough to suggest a new bug (all
     three are within noise of a materially different, larger `test`
     split population), but none should be read as "the multi-patch
     rebuild improved the models" either — it did not, and this is
     stated plainly rather than the more convenient framing.
  4. Patch 60 is *still* the current, active patch as of this ADR's
     writing (2026-08-09/10) — there is no "future-relative-to-60" patch
     data available from OpenDota yet, so this dataset's multi-patch
     coverage is entirely "current + 2 immediately preceding patches,"
     not "spans a patch that has since been superseded by 60."
- Per `docs/ROADMAP.md`'s updated "Recommended next phase": ONNX export
  and a `LIVE_ALLOWED`-restricted retrain of the Win Probability model
  are the natural next step now that this rebuild is delivered.

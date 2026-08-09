# ADR 0020: Item Recommendation Ranker baseline

## Status

Accepted

## Context

Third Fase 3 model, following the Win Probability baseline
(`docs/adr/0017`/`0018`) and Draft baseline (`docs/adr/0019`). Per
`docs/MASTER_PLAN.md` Module 11: model `P(next_item | current_state)`,
with an explicit warning against recommending "simply by winrate"
(survivor bias — the module's own Divine Rapier example: a high-winrate
item can just reflect that it's bought in already-winning situations).
Baseline progression specified: frequency-conditioned → rules → nearest
historical states, then LightGBM ranker / LambdaMART.

## Decision 1: task framing — ranking with LambdaMART, not multi-class classification

`dota-ml/references/evaluation.md` is explicit that ranking tasks must be
evaluated "on the actual candidate sets the model will see at
inference," which matches this use case directly (MASTER_PLAN's own UI
mockup shows a ranked top-3, not a single hard prediction). LightGBM's
native `LGBMRanker` (`objective="lambdarank"`) is literally the model
MASTER_PLAN names. A 114-way multi-class softmax was rejected: it needs
heavy class-imbalance handling and produces a single argmax, not the
ranked, scored top-3 the UI needs.

**Reconciling with MASTER_PLAN's "BKB 86%, Shiva 63%" display format**:
those are not calibrated probabilities from the ranker — LambdaMART
produces a relevance score, not P(purchase). This model's rank *position*
is what NDCG/MRR evaluate; any percentage shown in a future UI should
come from the separate frequency-conditioned popularity table (Decision
3), documented explicitly as a display convenience, not the ranker's own
evaluated output. Stated here so a future implementer doesn't
mistakenly treat the ranker's raw score as a probability.

## Decision 2: meaningful item vocabulary — data-driven, not hand-listed

Extracted from real `purchase_log` data (1863 matches from the Fase 2/3
acquisition), a first attempt hand-blacklisted consumables/wards/TP
scrolls, but **350,553 rows still slipped through** because a blacklist
missed several cheap-but-real intermediate components (`ogre_axe`,
`gauntlets`, `mithril_hammer`, `belt_of_strength`) that dominated
`purchase_log` by raw frequency without being real "next item" decisions.

Fixed by fetching OpenDota's own `GET /constants/items` (verified live,
cached at `data/opendota_item_constants.json`, 501 items) and using its
`qual` field: keep only `rare`/`epic`/`artifact`/`common` (129 items),
excluding `consumable`, `secret_shop` (raw stat items rarely a final
purchase), `component` (intermediate pieces, including surprisingly
`blink` itself — it's tagged `component` because it upgrades to
Overwhelming/Swift/Arcane Blink), and untagged recipes. Result: **139,355
(player, purchase) rows across 1862 matches, 114 unique meaningful
items** — a realistic distribution (Magic Wand, Aghanim's Shard, Power
Treads, BKB topping frequency, as expected).

A second real bug found the same way: **~2.3% of rows (1977/85,419 in
train) were repurchases** — `next_item` already present in
`items_owned_before` (e.g. a second Magic Wand-tier item). Since the
candidate pool structurally excludes already-owned items (Decision 4),
these rows could never have a valid relevant candidate and were silently
unscoreable. Fixed by excluding them at extraction time
(`stages/item_events.py`), verified by
`tests/test_item_ranker.py::TestRepurchaseRowsAreExcludedUpstream`.

## Decision 3: two baselines, per MASTER_PLAN's "frequency-conditioned" and "rules" tiers

**Baseline 1 (frequency-conditioned)**: `P(next_item | hero_id)`,
Bayesian-shrunk toward the train-split global item-frequency distribution
(`HERO_ITEM_PRIOR_STRENGTH = 20`) — the same shrinkage discipline
`docs/adr/0019` already established for the draft baseline's hero
winrate table, applied here per-item instead of to a single scalar.
Hero-alone (not hero+minute or hero+items-owned) was chosen deliberately:
1862 matches / ~120 heroes already gives thin per-hero samples: adding a
second conditioning dimension would worsen sparsity the same way the
draft baseline's (hero, patch) granularity already showed to be a limit.

**Baseline 2 ("rules")**: unconditional global item-popularity — "always
rank by how often this item is bought, ignoring hero entirely." This
project already ships a real, hand-curated rule engine
(`src/DotaAiCoach.RecommendationEngines/NextItemSelector.cs`/`ItemCatalog.cs`)
for the live C# app; porting its domain logic into Python would duplicate
curation rather than produce a new data point about the ML approach, so
it was **not** reimplemented here. Baseline 2's sole purpose is showing
hero-conditioning matters (mirrors Win Probability's constant-0.5
baseline existing only to show the single-feature model beats nothing).
A future comparison against the C# rule engine's *implicit* ranking is a
reasonable follow-up, explicitly deferred rather than silently dropped.

## Decision 4: candidate set — all meaningful items minus owned, no gold filter (v1.0 limitation)

The candidate pool for every row is the 114 meaningful items minus
`items_owned_before` — a **hard structural filter**, applied identically
at train and eval time, never something the model has to learn (verified
by `tests/test_item_ranker.py::TestCandidateSetExcludesOwnedItems`).

**No gold-affordability filter in v1.0** — `item_events.py` doesn't
extract minute-indexed gold state (unlike `stages/features.py`, which
already joins `gold_t` for the win-probability model); adding it would
require a real cross-pipeline join, deferred as real added scope rather
than done hastily. Consequence: at minute 3 the candidate pool still
includes `divine_rapier` (6200 gold, unaffordable), which the ranker must
learn to deprioritize purely from the hero+minute popularity feature
(nobody buys Rapier at minute 3) rather than from an explicit
affordability signal. This makes v1.0's NDCG numbers a **lower bound**
relative to a hypothetical gold-filtered v1.1 — stated as a limitation,
not closed in this pass.

## Decision 5: feature set — lean, all LiveAllowed/PublicPreflight

Five features per (state, candidate) row, intentionally smaller than the
win-probability model's 18 (this is a ranker over a small candidate
description, not a rich match-state classifier):

| feature | source | fairPlayClassification | justification |
|---|---|---|---|
| `minute` | `item_events.py` | LIVE_ALLOWED | the in-game clock is always visible |
| `n_items_owned_before` | `item_events.py` | LIVE_ALLOWED | own inventory is always visible |
| `item_popularity_for_hero` | train-split-only, Bayesian-shrunk (Baseline 1's table) | PUBLIC_PREFLIGHT | public build statistics, independent of the live match |
| `item_popularity_for_hero_minute_bucket` | train-split-only, nested-shrunk toward `item_popularity_for_hero` (same nested-shrinkage pattern as the draft baseline's matchup matrix shrinking toward the overall win rate) | PUBLIC_PREFLIGHT | captures item timing (boots early, BKB mid, situational late) without raw-minute sparsity |
| `item_global_popularity` | train-split-only | PUBLIC_PREFLIGHT | fallback signal for thin hero samples |

Unlike the win-probability baseline, **this model contains zero
`POSTGAME_ONLY` features** — every feature is legitimately knowable live,
worth noting as a positive contrast. `radiant_win`/`player_won` are
explicitly excluded (dota-ml leakage pattern 2: outcome-adjacent),
verified by `tests/test_item_ranker.py::TestNoOutcomeAdjacentFeatures`.
MASTER_PLAN's `Expected Win Uplift(item|state)` stretch goal is
**explicitly deferred** — computing it naively (correlate item purchase
with match outcome) would be exactly the survivor-bias trap the module's
own Divine Rapier example warns against; a real version needs a
causal/counterfactual framing, not naive correlation, and is out of scope
for this baseline.

## Decision 6: split reuse

Reuses the same 1863-match population and the same 4-way temporal
match-level split logic already established in `docs/adr/0017` (60/15/10/15
train/val/calib/test), recomputed independently from
`item_events.parquet`'s own `match_id` ordering
(`stages/item_dataset.py`) since the underlying match population and
temporal ordering are identical to the other two models' datasets.

## Results (actual numbers)

| | NDCG@3 | NDCG@5 | MRR |
|---|---|---|---|
| Baseline 2 (global popularity, unconditional "rule") — val | 0.191 | 0.231 | 0.236 |
| Baseline 1 (per-hero popularity, Bayesian-shrunk) — val | 0.409 | 0.460 | 0.435 |
| **LightGBM LambdaMART ranker — val** | 0.550 | 0.594 | 0.562 |
| **Final test (touched once)** | **0.544** | **0.587** | **0.557** |

This is the strongest result of the three Fase 3 models so far — the
ranker clearly beats both baselines by a wide margin, and hero-conditioning
alone (Baseline 1 vs 2) already more than doubles NDCG@3, confirming item
choice really is hero-dependent (unsurprising, but good to confirm
rather than assume).

**Segmented by minute bucket** (test): early (<15min) NDCG@3 **0.715**,
mid (15-30min) **0.466**, late (>30min) **0.351** — items are easiest to
predict early (most heroes follow similar, more standardized builds:
boots, Wand-tier items) and hardest late (situational, divergent
builds), the expected pattern.

**Domain sanity check** (ranker vs. Baseline 1 top-1 agreement rate,
chosen since a purely random check isn't meaningful for a ranking task —
see `docs/adr/0019`'s reasoning about needing a real, checkable direction):
**47.5% agreement**, vastly above the 0.9% chance rate (1/114) —
confirms the ranker learned real hero/timing structure rather than
producing degenerate output, without over-claiming precision this
specific number should have.

## Performance note

The first training attempt appeared to hang with no output for several
minutes and was killed. Root cause, found by isolating each pipeline
stage's timing separately: `_ndcg_and_mrr`'s original implementation
called `sklearn.metrics.ndcg_score` once per (query, k) inside a Python
`for` loop — with ~20,000+ queries × 2 k-values, tens of thousands of
individual sklearn calls, far too slow at this row count (candidate row
building itself was already confirmed fast — 9.2M rows in 16s). Rewrote
`_ndcg_and_mrr` using the closed-form NDCG for the single-relevant-
document-per-query case (true here: each row has exactly one correct
`next_item`), computing every query's relevant-item rank position via one
vectorized `groupby().rank()` call instead of a per-group loop — reduced
the full evaluation from an unbounded hang to ~1.5s per 2M rows. Full
training (9.19M train / 2.32M val / 2.29M test candidate rows) then
completed in under 2 minutes end to end, LightGBM's own fit taking 45s.

## Consequences

- Model saved at `models/item-ranker-v1.0-<runid>/` with the same
  `metadata.json`/`evaluation.json` reproducibility contract as the
  other two Fase 3 models.
- **Explicitly out of scope, not started**: gold-affordability candidate
  filtering (Decision 4's stated v1.1), `Expected Win Uplift` (Decision
  5, deferred with a causal-framing justification), a "nearest historical
  states" baseline tier (MASTER_PLAN lists it, but at this data scale it
  would be redundant with the frequency-conditioned baseline already
  built — skipped as low-value rather than silently omitted), ONNX
  export, live wiring into `RecommendationEngines`.
- `tests/test_item_ranker.py`'s six tests permanently guard: candidate-
  set correctness, train-only popularity tables, the repurchase-row
  exclusion, absence of outcome-adjacent features, and ranking-group
  integrity.
- With this ADR, all three baselines MASTER_PLAN's ML Baselines module
  calls for (Win Probability, Draft, Item Recommendation) now exist,
  each with an honestly-reported result: strong for win probability
  (AUC 0.839) and item ranking (NDCG@3 0.544), weak for draft alone
  (AUC 0.531) — a coherent, evidence-based picture of where this
  dataset's signal actually lives, useful for prioritizing future work.

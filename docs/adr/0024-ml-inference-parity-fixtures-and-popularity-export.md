# ADR 0024: ONNX parity fixtures and item-ranker popularity table export

## Status

Accepted

## Context

`docs/adr/0023`'s "Consequences" flagged two known limitations left open
by that pass:

1. **No true cross-language (Python-vs-C#) numeric parity fixture.**
   `docs/adr/0022`'s `onnx_export_metadata.json` recorded only a
   *summary* max-abs-diff statistic and its own six fixed test rows'
   *outputs* — not the raw *input feature vectors* that produced them.
   Without the inputs, C# tests could not reconstruct an equivalent call
   and compare against those exact numbers; `tests/DotaAiCoach.MlInference.Tests`
   could therefore only assert structural correctness (loads, output in a
   valid range, feature order not silently transposed), never exact
   numeric parity.
2. **Item Ranker's popularity lookup tables** (`model.pkl`'s
   `tables['global'|'hero'|'hero_minute']`, Bayesian-shrunk lookups
   computed by `item_ranker.py`) exist only inside a Python pickle.
   `IItemRankPredictor`'s input (`ItemRankInput`) requires the caller to
   supply the three popularity figures directly — a deliberate design
   choice at the time (`docs/adr/0023` Decision 5: the Feature Engine
   owns static reference data lookups), but the actual JSON/Parquet
   export of the table *values* was left explicitly open for "next
   phase."

This task is that next phase, scoped as: real parity fixtures + tests
(closes gap 1), and popularity table export + a judgment call on whether
`IItemRankPredictor`'s shape should change (closes gap 2). Per this
task's constraints: no wiring into `RecommendationEngines`/`UiOverlay`,
no `IWinProbabilityPredictor`/`IDraftScorePredictor` shape changes, no
existing model artifact modified.

## Decision 1: parity fixtures reuse `export_onnx.py`'s existing sampling logic, extended with real val-split rows

`tools/DotaAiCoach.Training/scripts/export_parity_fixtures.py` (new)
reuses `export_onnx.py`'s `_fixed_test_inputs` function directly (same 6
fixed rows: all-zero/missing-feature-fill, extreme-large-positive,
extreme-large-negative, mid-boundary, 2 representative random rows —
imported, not re-derived) rather than inventing a different sampling
approach. This mirrors `onnx-export.md`'s own checklist categories that
ADR 0022 already used.

Extended per model with **10 additional real, deterministic rows** drawn
from each model's own `val.parquet` (win-probability-live, draft-baseline)
or `build_candidate_rows` output (item-ranker) — selected by fixed
positional index (`range(0, 10)`), not randomly, so the fixture is
reproducible on a future regeneration. This gives 16 test cases per model
total, covering both synthetic edge cases and genuine dataset rows.

For each case, the fixture records:
- `input`: the full named feature dict, keyed by the model's own
  `feature_columns`/`onnx_export_metadata.json` `feature_order` names
  (`gold_adv_at_T`, `hero_winrate_diff`, `item_popularity_for_hero`,
  etc.) — not a re-paraphrase, the literal training-time column names.
- `expected_raw_output`: the pre-export (pickled `model.pkl`) model's own
  prediction for that exact input.
- `expected_calibrated_output`: the model's own `calibrator.pkl`
  (`IsotonicRegression`, fit on the `calib` split) applied to the raw
  output — `null` for item-ranker, which has no calibration step
  (`item_ranker.py` is a ranking task, not a probability).
- `expected_onnx_output`: running `model.onnx` directly via a fresh
  `onnxruntime.InferenceSession` for that exact input — not trusted from
  the pre-export model, independently computed.
- `raw_vs_onnx_abs_diff`: measured per-case, not assumed.

Three JSON files written to
`tools/DotaAiCoach.Training/tests/fixtures/onnx_parity/`:
`win_probability_live_parity.json`, `draft_baseline_parity.json`,
`item_ranker_parity.json`, each with a `metadata` block (source model id,
feature order, case count, the achieved max-abs-diff across all cases,
generation timestamp) plus the `cases` array.

## Decision 2: actual measured parity results (the load-bearing numbers)

Run for real (`python scripts/export_parity_fixtures.py`), not assumed:

| model | n cases | max abs diff (ONNX vs. pre-export raw) | `export_onnx.py`'s own tolerance | inside tolerance |
|---|---|---|---|---|
| win-probability-live | 16 | **6.909e-08** | 1e-4 | yes, by ~3 orders of magnitude |
| draft-baseline | 16 | **4.703e-08** | 1e-4 | yes, by ~3 orders of magnitude |
| item-ranker | 16 | **4.403e-06** | 1e-3 | yes, by ~2 orders of magnitude |

These are consistent with (same order of magnitude as, in most cases
tighter than) ADR 0022's own 6-fixed-row validation numbers
(6.91e-08 / 1.09e-08 / 3.30e-06), which is expected — this fixture set is
a superset of essentially the same edge-case sampling strategy plus real
data rows, run against the identical `.onnx` artifacts ADR 0022 already
validated. No tolerance was loosened to make anything pass; both
tolerances used in the C# assertions below are the exact tolerances
`export_onnx.py` itself already declared for each model, not a new
figure invented for this pass.

**Calibration parity**: the C# `IsotonicCalibrator`'s reconstruction is
exact piecewise-linear interpolation over the same breakpoints Python's
`IsotonicRegression` produces (`docs/adr/0022`/`0023` Decision 4) —
tested at a tight `1e-6` tolerance (vs. `1e-4` for the ONNX-graph
parity), since this arithmetic has no floating-point-precision-sensitive
graph execution step the way the ONNX call does; it is a direct `clip`
+ linear-interpolate over doubles both sides. All calibration-parity
test cases pass at this tighter tolerance.

## Decision 3: C# tests load fixtures via `System.Text.Json`, kept alongside (not replacing) existing structural tests

Fixtures copied to
`tests/DotaAiCoach.MlInference.Tests/Fixtures/OnnxParity/*.json`,
registered via `<None Include="Fixtures\**" CopyToOutputDirectory="PreserveNewest" />`
in the `.csproj` — the same `CopyToOutputDirectory` pattern
`DotaAiCoach.Gsi.Tests`'s `Fixtures\Gsi\` convention and this project's
own bundled `Models\**\*.json` pattern already use, so no new convention
was introduced.

Three new test classes (`WinProbabilityPredictorParityTests`,
`DraftScorePredictorParityTests`, `ItemRankPredictorParityTests`), each
using an `xUnit` `[Theory]`/`MemberData` to run one assertion per fixture
case (16 raw-parity cases + 16 calibration-parity cases per calibrated
model, 16 raw-parity cases only for item-ranker), plus one fixture
sanity test per model asserting the recorded tolerance is actually below
the asserted tolerance (protects against a future fixture regeneration
silently loosening the bar without anyone noticing). All pre-existing
structural tests (`WinProbabilityPredictorTests`,
`DraftScorePredictorTests`, `ItemRankPredictorTests`) were kept entirely
unchanged, per this task's explicit instruction — new tests were added
alongside, nothing replaced.

Result: `DotaAiCoach.MlInference.Tests` grew from **17 to 112 tests**
(95 new: 3 parity classes × ~30 cases each + 6 new
`HeroItemPopularityTableTests`, minus a few shared sanity tests), all
green.

## Decision 4: popularity table export — actual pickled shape verified live, not assumed from ADR 0020's prose

Per this task's live-verification discipline, `item_ranker.py`'s
`compute_item_popularity_tables` was read in full (not just ADR 0020's
summary) before writing the export script. Actual shape, confirmed both
by reading the code and by a runtime `assert` in the export script
itself (`export_popularity_table.py`) that checks the real key/value
types before writing JSON:

- `tables["global"]`: `dict[str, float]` — flat, keyed by item name.
- `tables["hero"]`: `dict[tuple[int, str], float]` — keyed by
  `(hero_id, item_name)`.
- `tables["hero_minute"]`: `dict[tuple[int, str, str], float]` — keyed
  by `(hero_id, minute_bucket, item_name)`, `minute_bucket` in
  `{"early", "mid", "late"}` per `item_ranker.py`'s `_minute_bucket`.

Since tuple keys are not valid JSON object keys, the export re-nests each
table into a JSON-native shape instead of flattening to a composite
string key (e.g. `"121:item_name"`), so the JSON stays directly walkable
on the C# side:

```json
{
  "all_items": ["abyssal_blade", ...],
  "global": { "abyssal_blade": 0.0038... },
  "hero": { "1": { "abyssal_blade": 0.0454... } },
  "hero_minute": { "1": { "early": { "abyssal_blade": 0.0011... } } }
}
```

Written to
`tools/DotaAiCoach.Training/models/item-ranker-v1.0-20260810T032341Z/hero_item_popularity.json`
via `tools/DotaAiCoach.Training/scripts/export_popularity_table.py`
(new, separate script rather than folded into
`export_parity_fixtures.py` — the two are genuinely different jobs, one
exports a static reference table, the other exports fixtures for a
different test suite, and each has an independently-useful failure mode
to distinguish if either breaks).

**Actual file size: 2,694,988 bytes (2.63 MB)** — 114 items, 14,478
`hero` entries, 43,434 `hero_minute` entries. Every one of 114 heroes has
an entry for every one of 114 items in both the `hero` and `hero_minute`
tables (dense, not sparse) — confirmed live (no missing-hero fallback
case was found to test against; the fallback-to-global code path is
tested with a synthetic unknown hero id instead, see Decision 5).

Copied to `src/DotaAiCoach.MlInference/Models/item-ranker/hero_item_popularity.json`
— already covered by the existing `<Content Include="Models\**\*.json" CopyToOutputDirectory="PreserveNewest">`
glob in `DotaAiCoach.MlInference.csproj` (verified, no `.csproj` change
needed).

## Decision 5: `IItemRankPredictor`'s shape is kept unchanged — a separate `HeroItemPopularityTable` loader was added instead

This is the genuine judgment call this task asked for. Two options were
considered:

**(a) Keep `IItemRankPredictor` as-is; add a separate loader class** the
Feature Engine (or any future caller) can use to look up the three
popularity figures, now that a real JSON export exists (previously it
didn't, so the original design was correct given what was available at
the time).

**(b) Change `ItemRankInput` to drop the three popularity fields and have
`ItemRankPredictor` load/look them up internally**, since the data now
genuinely is bundled with `MlInference` and not just conceptually
available.

**Chosen: (a).** Concrete reasoning, not a default:

1. **Size is real, not hypothetical.** `hero_item_popularity.json` is
   2.63 MB with ~58,000 total lookup entries (114 + 14,478 + 43,434).
   Eagerly parsing this into memory on *every* `ItemRankPredictor`
   construction — including in tests that only need ONNX-graph behavior,
   not popularity data at all (e.g. the existing structural/feature-order
   tests, and the new parity tests, none of which use popularity
   lookups) — is real, avoidable cost. `ItemRankPredictor`'s own current
   constructor cost is dominated by `InferenceSession` construction over
   a ~105 KB file; folding in a 2.63 MB JSON parse on every construction
   would change that cost profile for every caller, not just ones that
   need it.
2. **ADR 0023 Decision 5's original reasoning survives the new
   evidence.** The Feature Engine (per `dota-architecture`'s layering) is
   still the layer that owns "`MatchState` + static reference data →
   normalized features." A popularity lookup table is exactly that kind
   of static reference data — the fact that an export now exists doesn't
   change which architectural layer should own consuming it; it only
   removes the *practical* blocker (no export existed) that made the
   caller-supplied design a stopgap-shaped choice at the time. Now that
   `HeroItemPopularityTable` exists as a real, correct, tested loader,
   the Feature Engine (or any future caller) can construct one, cache it
   for the lifetime of a live session, and populate `ItemRankInput`'s
   existing fields exactly as designed — the seam ADR 0023 built is
   still the right seam.
3. **A caller may legitimately want to look up the same value many
   times** across a single live match (once per item-ranking call) —
   forcing `ItemRankPredictor` itself to own the table means either
   re-parsing 2.63 MB per predictor instance or introducing predictor-
   level caching/singleton lifetime concerns that don't belong in an
   ONNX-inference wrapper. Keeping the loader separate lets its lifetime
   be the caller's decision, not baked into the predictor.

`HeroItemPopularityTable` (new, `src/DotaAiCoach.MlInference/HeroItemPopularityTable.cs`)
exposes `GlobalPopularity(item)`, `HeroPopularity(heroId, item)`,
`HeroMinuteBucketPopularity(heroId, bucket, item)`, and a static
`MinuteBucket(minute)` helper matching `item_ranker.py`'s `_minute_bucket`
boundaries exactly (`<15` early, `<30` mid, else late). Fallback
semantics were verified against `build_candidate_rows`'s actual
`fillna` calls, not assumed: both the `hero` and `hero_minute` tables
fall back directly to `global` popularity on a miss — `hero_minute` does
**not** fall back to `hero` first, matching the real two-column
`fillna(item_global_popularity)` calls in `item_ranker.py`, not a
plausible-sounding three-stage chain.

This is **additive** — `IItemRankPredictor`/`ItemRankInput` are
byte-for-byte unchanged from ADR 0023. `ItemRankPredictor.cs` itself was
not modified.

## Decision 6: popularity table test uses independently-verified values, not a round-trip check

`HeroItemPopularityTableTests` asserts against literal numeric values
(e.g. `HeroPopularity(1, "abyssal_blade")` == `0.045445980470149146`)
read directly out of the generated JSON via a one-off Python inspection
at fixture-generation time — not derived from or re-computed by the C#
loader itself, which would only prove internal consistency, not
correctness against the Python source of truth. Fallback-to-global
behavior (unknown hero id, unknown minute bucket) is tested against the
loader's own `GlobalPopularity` call for the same item, which is
appropriate here since fallback correctness is a code-path assertion
(does the miss path route to the right table), not a numeric-parity
assertion.

## Consequences

- New: `tools/DotaAiCoach.Training/scripts/export_parity_fixtures.py`,
  `tools/DotaAiCoach.Training/scripts/export_popularity_table.py`.
- New: `tools/DotaAiCoach.Training/tests/fixtures/onnx_parity/{win_probability_live_parity,draft_baseline_parity,item_ranker_parity}.json`.
- New:
  `tools/DotaAiCoach.Training/models/item-ranker-v1.0-20260810T032341Z/hero_item_popularity.json`
  (2.63 MB) — the only new file written under an existing timestamped
  model directory; nothing in that directory was modified or deleted.
- New: `src/DotaAiCoach.MlInference/HeroItemPopularityTable.cs`.
- New: `src/DotaAiCoach.MlInference/Models/item-ranker/hero_item_popularity.json`
  (copy of the above).
- New:
  `tests/DotaAiCoach.MlInference.Tests/{OnnxParityFixture,WinProbabilityPredictorParityTests,DraftScorePredictorParityTests,ItemRankPredictorParityTests,HeroItemPopularityTableTests}.cs`,
  `tests/DotaAiCoach.MlInference.Tests/Fixtures/OnnxParity/*.json` (copies
  of the fixtures above).
- Modified: `tests/DotaAiCoach.MlInference.Tests/DotaAiCoach.MlInference.Tests.csproj`
  (added the `Fixtures\**` `CopyToOutputDirectory` entry — the only
  `.csproj` change in this pass; `DotaAiCoach.MlInference.csproj`'s
  existing `Models\**\*.json` glob already covered the new popularity
  JSON without modification).
- **Not touched, confirmed**: `IWinProbabilityPredictor`,
  `IDraftScorePredictor`, `IItemRankPredictor`,
  `WinProbabilityPredictor.cs`, `DraftScorePredictor.cs`,
  `ItemRankPredictor.cs`, `src/DotaAiCoach.RecommendationEngines/`,
  `src/DotaAiCoach.UiOverlay/` — no wiring/interface work done, per this
  task's explicit scope.
- Full test suite, run for real: Python `pytest` **59 passed** (all
  pre-existing, no new Python tests needed — the two new scripts are
  standalone export utilities, not library code with its own unit-test
  surface, consistent with `export_onnx.py`'s own precedent of being
  validated by its own `__main__`-driven run rather than a dedicated
  test file). `dotnet build` **0 warnings, 0 errors**, 21 projects.
  `dotnet test` **303 tests across 9 test projects, all passing**
  (up from 208 in ADR 0023: +95 in `DotaAiCoach.MlInference.Tests`,
  0 changes elsewhere).
- **Known limitations, reported honestly**:
  1. The 10 "real" rows per fixture are deterministic by *positional*
     index (first 10 rows of `val.parquet`/candidate build), not
     randomly sampled or curated for interesting/extreme real-world
     values — they exist to prove parity holds on genuine dataset rows,
     not to stress-test unusual real inputs specifically. The 6 synthetic
     edge cases (inherited from `export_onnx.py`) remain the primary
     stress-test coverage.
  2. `hero_item_popularity.json` is a **snapshot** tied to
     `item-ranker-v1.0-20260810T032341Z` — regenerating it after a
     retrain requires re-running `export_popularity_table.py` and
     re-copying into `MlInference/Models/`, the same manual-refresh
     workflow ADR 0023 already established for `.onnx`/
     `onnx_export_metadata.json`. No automated sync mechanism exists; a
     future retrain-and-redeploy pipeline should account for this.
  3. This pass remains **export/fixture/test work only** — no wiring
     into `RecommendationEngines`/`UiOverlay`, per this task's explicit
     scope. `HeroItemPopularityTable` is ready for a future Feature
     Engine consumer but has no live caller yet.

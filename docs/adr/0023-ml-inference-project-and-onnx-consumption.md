# ADR 0023: DotaAiCoach.MlInference project and ONNX model consumption

## Status

Accepted

## Context

`docs/adr/0022`'s "Recommended next phase" (restated in
`docs/ROADMAP.md`) named this task explicitly: stand up
`DotaAiCoach.MlInference` per `dota-architecture`'s
`references/dotnet-solution-structure.md`, load the three exported ONNX
models (`win-probability-live`, `draft-baseline`, `item-ranker`), and
replicate each LogReg model's isotonic calibration in C#. This task is
scoped as **project + predictor implementation + tests only** — no
wiring into `DotaAiCoach.RecommendationEngines`, no UI changes.

Per `.claude/skills/dota-architecture` (`SKILL.md` and all four
references read in full before any code was written).

## Decision 1: project shape — `MlInference → Domain` only

`src/DotaAiCoach.MlInference/DotaAiCoach.MlInference.csproj` references
`DotaAiCoach.Domain` and the `Microsoft.ML.OnnxRuntime` NuGet package
(version 1.20.1, latest stable at the time of this pass), and nothing
else — matching `dependency-rules.md`'s `Ml.Inference (ONNX Runtime) →
Domain` row exactly. `Microsoft.ML.OnnxRuntime` is referenced only in
this project's `.csproj`; no other `src` project references it. This
keeps `Ml.Inference` testable headless (no GSI, no WPF) per
`new-module-checklist.md`'s "does this module need to be testable
headless" gate, and keeps the ONNX Runtime dependency from leaking into
`RecommendationEngines`, per `dotnet-solution-structure.md`'s explicit
guidance to depend on a Domain-level abstraction instead.

The project was added to `DotaAiCoach.sln` under the existing `src`
solution folder, matching the existing GUID-based registration format
(new GUIDs minted for the project and its test project, no divergent
format introduced). `TargetFramework net8.0`, `ImplicitUsings enable`,
`Nullable enable` — identical to `FeatureEngine`/`RecommendationEngines`'s
existing `.csproj` conventions.

## Decision 2: Domain-level predictor abstractions

Three new files were added additively to `DotaAiCoach.Domain` (no
existing Domain file touched), under a new `MlInference` folder
(`src/DotaAiCoach.Domain/MlInference/`) alongside Domain's existing
`Match`/`Events`/`Recommendations` folders:

- `IWinProbabilityPredictor.Predict(WinProbabilityInput) ->
  WinProbabilityOutput` — 12 named double fields (matching
  `train_live.py`'s `FEATURE_COLUMNS`) plus a nullable `bool?
  FirstBloodRadiant` (true/false/unknown, encoded 1.0/0.0/0.5 by the
  implementation, matching training-time treatment of missing values).
  Output carries both `RawRadiantWinProbability` and
  `CalibratedRadiantWinProbability` so a caller can inspect either.
- `IDraftScorePredictor.Predict(DraftScoreInput) -> DraftScoreOutput` —
  two named double fields (`HeroWinrateDifference`,
  `MatchupAdvantage`), matching the actual `draft-baseline` ONNX input
  schema (2 features, not the 13 the win-probability model has — verified
  directly from `onnx_export_metadata.json`, not assumed from the
  win-probability shape).
- `IItemRankPredictor.Predict(ItemRankInput) -> ItemRankOutput` — 5 named
  double fields (`MinuteOfMatch`, `ItemsOwnedBeforeCount`, and the three
  popularity lookups), returning a single raw `RankScore` (no
  calibration — ranking model).

None of these types reference ONNX/tensor/OnnxRuntime types — plain
records with named `double`/`bool?` fields, per
`new-module-checklist.md`'s "Domain types stay infrastructure-free"
requirement. Field order in the record does not need to match the
model's tensor order; each `MlInference` implementation is solely
responsible for that mapping, and does so directly against the literal
`feature_order` array in each model's own `onnx_export_metadata.json`
rather than against this ADR's paraphrase of it.

## Decision 3: model artifact placement — bundled as build content under `Models/`

All three `.onnx` files are small (472 bytes for `draft-baseline`, 700
bytes for `win-probability-live`, 104KB for `item-ranker`) — copying
them into the shipped application is trivial from a size perspective.
They were copied (along with each model's `onnx_export_metadata.json`,
needed at runtime for calibration breakpoints) into
`src/DotaAiCoach.MlInference/Models/<model-name>/`:

```
src/DotaAiCoach.MlInference/Models/
├── win-probability-live/{model.onnx, onnx_export_metadata.json}
├── draft-baseline/{model.onnx, onnx_export_metadata.json}
└── item-ranker/{model.onnx, onnx_export_metadata.json}
```

Registered in the `.csproj` as `<Content Include="Models\**\*.onnx"
CopyToOutputDirectory="PreserveNewest">` (and the same for `*.json`), so
they land next to the compiled assembly at build/publish time with zero
runtime network access required, per `dotnet-solution-structure.md`'s
"local-first... runnable without network access for the live coaching
path" requirement. `ModelPathResolver` resolves paths as
`AppContext.BaseDirectory/Models/<model-name>/<file>` at runtime.
`tools/DotaAiCoach.Training/models/*` (the original, timestamped
artifact directories) were left untouched and read-only — the files
under `src/.../Models/` are copies, not moves, so the training output
remains the single source of truth and this project's bundled copies can
be refreshed by re-running the same copy step against a newer trained
artifact.

The `onnx_export_metadata.json` files are bundled in full (not trimmed
down to just the calibration block) so a human debugging in production
has the full validation/schema record available, even though the C# code
(`OnnxExportMetadata.cs`) only deserializes the `calibration_replication`
field it actually needs.

## Decision 4: calibration replication as C# reading the JSON sidecar directly, not a hand-copied constant table

`IsotonicCalibrator` implements the exact reconstruction recipe from
`docs/adr/0022`: clip `x` to `[min(x_thresholds), max(x_thresholds)]`,
then linearly interpolate `y_thresholds` over `x_thresholds` at the
clipped `x` (`np.interp` semantics, including its behavior at duplicate
`x` values — a vertical step returns the left-hand-scanned segment's
value, verified directly against `numpy.interp`'s actual behavior rather
than assumed).

Rather than hand-transcribing the 15-58-point breakpoint arrays from
`onnx_export_metadata.json` into a C# constant array (a duplicate that
could silently drift from the authoritative Python-written file on a
future retrain), `WinProbabilityPredictor`/`DraftScorePredictor` read
`calibration_replication.x_thresholds`/`y_thresholds` directly from the
bundled `onnx_export_metadata.json` at construction time via
`OnnxExportMetadata.Load(...)`. This keeps the single source of truth in
one place — re-running `export_onnx.py` and re-copying the JSON sidecar
is sufficient to pick up a recalibrated model, no C# code change needed.

## Decision 5: item-ranker popularity table gap — confirmed, and resolved by pushing the tables to the caller

Per the task brief's instruction to verify directly rather than trust
the paraphrase: the `item-ranker-v1.0-20260810T032341Z/` model directory
was listed and contains exactly `calibrator.pkl` (absent — no
calibrator for this model, matching `metadata.json`), `evaluation.json`,
`metadata.json`, `model.onnx`, `model.pkl`, and
`onnx_export_metadata.json` — **no JSON/Parquet sidecar of the actual
popularity table values exists anywhere in the directory.**
`onnx_export_metadata.json`'s `popularity_tables_not_exported_note`
confirms this is a known, already-flagged gap: the Bayesian-shrunk
`tables['global'|'hero'|'hero_minute']` dictionaries live only inside
`model.pkl` (a Python pickle), not accessible from C# without either a
Python interop bridge or a fresh export step — both out of scope for
this pass per the task brief.

**Resolution**: `IItemRankPredictor`'s input type
(`ItemRankInput`) requires the caller to supply
`ItemPopularityForHero`, `ItemPopularityForHeroMinuteBucket`, and
`ItemGlobalPopularity` directly as plain `double` fields — the predictor
does not own, load, or fabricate any popularity table itself. This is a
deliberate design decision, not a stopgap: per `dota-architecture`'s
layering, the Feature Engine is the layer that already owns "MatchState
+ static reference data → normalized features," and a popularity lookup
table is exactly that kind of static reference data. Fabricating
placeholder table values inside `MlInference` would have been worse than
leaving the gap explicit, since a silently-wrong lookup table is much
harder to notice than a compiler-enforced missing input. The real
resolution — exporting `model.pkl`'s `tables` dict to a JSON/Parquet
sidecar the Feature Engine can load — remains explicitly open for the
next wiring phase (see "Open questions for next phase" below).

## Decision 6: test project and Architecture Tests coverage

`tests/DotaAiCoach.MlInference.Tests/` mirrors
`RecommendationEngines.Tests`'s conventions (xUnit 2.5.3, FluentAssertions
6.12.0, `net8.0`, no UI framework reference). Coverage:

- **Model-loads-and-predicts tests** for all three predictors (loads the
  bundled `.onnx`, runs a valid input, asserts the output is in a sane
  range/finite).
- **Feature-order/transposition tests** for all three predictors:
  constructs pairs of inputs that isolate a single feature at a large
  magnitude and confirms the two predictions diverge — this is the test
  category most directly protecting against a silent feature-order bug,
  the failure mode `onnx-export.md`/ADR 0022 called out as the hardest to
  detect.
- **Isotonic calibration unit tests** (`IsotonicCalibratorTests`) against
  a small hand-built breakpoint table (not copied from a real model, so
  the arithmetic is easy to verify by hand): exact value at a breakpoint,
  linear interpolation between breakpoints, duplicate-`x` vertical-step
  behavior (confirmed against real `np.interp` semantics — left-hand
  value at an exact duplicate match), clip behavior below the minimum and
  above the maximum threshold, and two constructor-validation tests
  (mismatched-length and empty arrays both throw).
- `DotaAiCoach.Architecture.Tests/DependencyGraphTests.cs` gained two new
  tests: `MlInference_references_no_infrastructure_beyond_OnnxRuntime`
  (fails if `MlInference` ever references `Gsi`/`FeatureEngine`/
  `RecommendationEngines`/`UiOverlay`/WPF assemblies) and
  `RecommendationEngines_does_not_reference_OnnxRuntime_directly` (fails
  if `RecommendationEngines` ever references `Microsoft.ML.OnnxRuntime`
  or `DotaAiCoach.MlInference` directly — the Domain-interface boundary
  from Decision 2 must hold even once wiring happens next phase).

**Parity test honesty**: the task brief asked for a true
cross-language (Python-vs-C#) parity fixture reusing ADR 0022's fixed
test rows. ADR 0022's `onnx_export_metadata.json` records the *summary*
statistic (max abs diff) and its own six fixed-row probabilities/scores,
but those six rows' raw *input feature values* are not recorded anywhere
in the model directory — only their resulting outputs are. Without the
input vectors, this pass cannot reconstruct an equivalent C# call and
compare against those exact recorded numbers; fabricating plausible input
values and asserting against the recorded outputs would not be a genuine
parity check, just a coincidence-shaped one. **Reported honestly**: no
true cross-language parity fixture was available this pass. What was
validated instead is structural correctness — each predictor loads its
real bundled `.onnx` file (the same file ADR 0022 already validated
against its Python original), produces output in the valid probability/
score range, and the feature-order tests above give confidence the
tensor mapping is not silently transposed. A future pass wanting a true
fixture would need `export_onnx.py`'s validation step to also dump its
six fixed input rows into `onnx_export_metadata.json` (a one-line
addition on the Python side) so C# tests can assert against exact
recorded numbers instead of only ranges/direction.

## Consequences

- `src/DotaAiCoach.MlInference/`: new project — `DotaAiCoach.MlInference.csproj`,
  `WinProbabilityPredictor.cs`, `DraftScorePredictor.cs`,
  `ItemRankPredictor.cs`, `IsotonicCalibrator.cs`, `OnnxExportMetadata.cs`,
  `ModelPathResolver.cs`, plus `Models/<model-name>/{model.onnx,
  onnx_export_metadata.json}` for all three models.
- `src/DotaAiCoach.Domain/MlInference/`: new, additive-only —
  `IWinProbabilityPredictor.cs`, `IDraftScorePredictor.cs`,
  `IItemRankPredictor.cs`. No existing Domain file modified.
- `tests/DotaAiCoach.MlInference.Tests/`: new — 17 tests, all green.
- `tests/DotaAiCoach.Architecture.Tests/`: 2 new tests added to
  `DependencyGraphTests.cs`; `.csproj` gained `ProjectReference`s to
  `MlInference` and `RecommendationEngines` needed to exercise them.
- `DotaAiCoach.sln`: two new project entries (`DotaAiCoach.MlInference`
  under the `src` folder, `DotaAiCoach.MlInference.Tests` under the
  `tests` folder), following the existing GUID-registration format
  exactly.
- **Full solution**: `dotnet build` succeeds (0 warnings, 0 errors, 21
  projects); `dotnet test` passes **208 tests across 9 test projects**
  (36 PlayerProfile + 20 Domain + 17 MlInference [new] + 33 OpenDota + 11
  FeatureEngine + 14 FairPlay + 32 RecommendationEngines + 14
  Architecture + 31 Gsi), zero failures, no pre-existing test needed to
  change.
- **Known limitations, reported honestly**:
  1. **No true cross-language parity fixture** — see Decision 6's parity
     test honesty note. Structural correctness (loads, valid range,
     feature order) was validated; exact-number parity against ADR
     0022's Python-computed values was not, because the underlying input
     vectors for those six rows are not recorded anywhere on disk.
  2. **Item Ranker's popularity tables remain unexported** — Decision 5's
     gap is resolved at the API-design level (the predictor doesn't
     pretend to own data it doesn't have) but the actual table *values*
     still only exist inside `model.pkl`. Whoever wires `IItemRankPredictor`
     into a live Feature Engine next must either export the tables to a
     JSON/Parquet sidecar from Python, or implement equivalent live
     popularity tracking directly in C# — this ADR does not resolve which.
  3. **No wiring into `RecommendationEngines`** — this pass is
     intentionally scoped to the predictor implementations and their
     Domain abstractions only. `RuleRecommendationEngine` (the sole
     existing recommendation engine, 4 rules: next-item,
     affordability/farm-until-affordable, buyback warning, power spike)
     and `LiveMessageBudget` (anti-spam cooldowns/change-threshold
     bucketing, `RecommendationEngines/LiveMessageBudget.cs`) were read
     but not modified. Open design questions for that next step:
     - **Override, blend, or supplement?** Should an ML win-probability
       reading ever override a rule's recommendation, blend into a rule's
       `Score` as an additional signal, or only supplement the existing
       4 rules as a new, separate rule (e.g. a "you are heavily favored/
       unfavored" message)? The 4 existing rules are all rule-based
       Dota-domain logic with no ML signal today — introducing one for
       the first time is a design decision this ADR deliberately leaves
       open rather than deciding unreviewed.
     - **LiveMessageBudget interaction**: any new ML-driven recommendation
       would need a `MessageKey`/`Priority`/`Score` shape compatible with
       `LiveMessageBudget.Select()`'s existing cooldown/change-threshold/
       max-3-messages gating (`dota-live-coach`'s anti-spam requirement)
       — a win-probability swing is a continuous value, not the
       discrete state `AffordabilityRule`'s 200g-band bucketing uses, so
       the "change threshold" bucketing strategy for a continuous ML
       signal needs its own design rather than reusing an existing rule's
       bucketing verbatim.
     - Both open questions are explicitly deferred to the next phase
       (see `docs/ROADMAP.md`'s updated "Recommended next phase"), not
       decided implicitly by this pass's scope.

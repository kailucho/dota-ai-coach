# .NET Solution Structure

This project is a **local-first Windows desktop application** — it runs
on the player's machine, reads their own GSI feed and screen, and shows
an overlay/voice UI. There is no server component implied by "Data
Platform" — it's a local data-access/storage layer, not a hosted service.
Design the solution structure around that: everything except the offline
`Training` project should be shippable as part of one desktop
application, runnable without network access for the live coaching path
(historical-data features degrade gracefully offline, they don't block
core functionality).

## Suggested project layout

```
DotaAiCoach.sln
├── src/
│   ├── DotaAiCoach.Domain/                 (MatchState, features, recommendation
│   │                                         models, dota-domain concepts — no
│   │                                         infrastructure references)
│   ├── DotaAiCoach.Gsi/                    (GSI listener, parser, normalizer)
│   ├── DotaAiCoach.ComputerVision/         (screen capture, CV pipeline)
│   ├── DotaAiCoach.StateMerger/            (reconciles Gsi + ComputerVision → MatchState)
│   ├── DotaAiCoach.FeatureEngine/          (MatchState → normalized features)
│   ├── DotaAiCoach.RecommendationEngines/  (feature-consuming recommendation logic
│   │                                         + ONNX model consumption)
│   ├── DotaAiCoach.RecommendationOrchestrator/
│   ├── DotaAiCoach.MlInference/            (ONNX Runtime wrapper, model loading)
│   ├── DotaAiCoach.DataPlatform/           (local storage + external API clients,
│   │                                         e.g. OpenDota client, cached locally)
│   ├── DotaAiCoach.FairPlay/               (classification types/attributes,
│   │                                         shared across layers that need to
│   │                                         tag data with a FairPlayClassification)
│   └── DotaAiCoach.UiOverlay/              (WPF or WinUI app: overlay rendering,
│                                             voice output, settings)
├── tools/
│   └── DotaAiCoach.Training/               (offline model training — not part of
│                                             the shipped app; separate solution or
│                                             solution folder, may use Python/ML.NET/
│                                             a separate toolchain, exports ONNX)
└── tests/
    ├── DotaAiCoach.Domain.Tests/
    ├── DotaAiCoach.Gsi.Tests/              (fixture-based, see dota-gsi)
    ├── DotaAiCoach.ComputerVision.Tests/
    ├── DotaAiCoach.FeatureEngine.Tests/
    ├── DotaAiCoach.RecommendationEngines.Tests/
    ├── DotaAiCoach.FairPlay.Tests/         (cross-cutting FairPlay assertions,
    │                                         see dota-fairplay)
    └── DotaAiCoach.Architecture.Tests/     (dependency-graph enforcement,
                                              see dependency-rules.md)
```

## Why this shape

- **`Domain` has no project references** except perhaps a shared
  `FairPlay` types project (classification enums/attributes are domain-
  adjacent, not infrastructure) — this is what makes rule 1 checkable at
  a glance: open `Domain.csproj`, see zero infrastructure `ProjectReference`
  entries.
- **`UiOverlay` is the only project referencing WPF/WinUI assemblies.**
  Every other project — including `MlInference`, `RecommendationEngines`,
  `RecommendationOrchestrator` — should build and test with `dotnet test`
  in a headless CI agent with no desktop session, which is also what
  makes rule 4 mechanically checkable.
- **`Training` lives under `tools/`, separate from `src/`**, signaling
  clearly that it's not part of the shipped application and may have a
  different (heavier, possibly cross-platform/Python-involving) toolchain
  than the rest of the solution. Its only contract with the rest of the
  system is: it produces `.onnx` files that `MlInference` can load.
- **`Architecture.Tests` is a real test project**, not just a convention
  — see `dependency-rules.md`'s "Enforcement" section for what it should
  assert. A reasonable implementation uses reflection over each built
  assembly's referenced assemblies and fails the build if a forbidden
  edge appears, which catches violations at CI time rather than relying
  on `.csproj` review.

## ONNX Runtime specifics

- Reference the `Microsoft.ML.OnnxRuntime` NuGet package (or the
  DirectML-backed variant if GPU inference on the player's machine is
  desired) only in `MlInference`, not scattered across
  `RecommendationEngines` — `RecommendationEngines` should depend on a
  Domain-level abstraction (e.g. an `IModelPredictor` interface) that
  `MlInference` implements, keeping ONNX Runtime specifics from leaking
  into engine logic and keeping engines testable with a fake predictor.
- Model files (`.onnx`) are build artifacts from `Training`, checked in
  or fetched as part of the build/release process — decide and document
  which, but either way `MlInference` should load them from a well-known
  local path/embedded resource, not require network access at runtime for
  the live coaching path to function.

## Local-first implications for Data Platform

- Historical/external API data (OpenDota etc.) should be cached locally
  (e.g. SQLite or a simple local file cache) so the application degrades
  gracefully offline rather than failing live features that don't
  actually need network access.
- Keep `DataPlatform`'s external API client code isolated from its local
  storage/caching code where practical — the caching layer is what
  `RecommendationEngines`/`Training` should generally depend on, not the
  raw API client, so offline behavior and rate-limiting stay centralized.

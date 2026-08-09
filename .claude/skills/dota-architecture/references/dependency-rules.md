# Dependency Rules

These rules are about project/assembly references — "may project A's code
reference a type from project B" — not about runtime data flow (data can
flow in more directions than references should point; e.g. the State
Merger *processes* GSI and CV output without GSI/CV referencing it back).

## The allowed dependency graph

```
Domain (MatchState, features, recommendation logic, dota-domain concepts)
  ← referenced by everything; references nothing infrastructure-specific

Gsi.Infrastructure          → Domain
ComputerVision.Infrastructure → Domain
StateMerger                 → Domain, Gsi.Infrastructure (types only), ComputerVision.Infrastructure (types only)
FeatureEngine                → Domain
RecommendationEngines        → Domain, FeatureEngine, Ml.Inference (ONNX)
RecommendationOrchestrator   → Domain, RecommendationEngines
DataPlatform                 → Domain (for shared reference-data types only), external API clients
Training                     → DataPlatform (offline-only project, not shipped in the app)
Ml.Inference (ONNX Runtime)  → Domain
Ui.Overlay (WPF/WinUI)       → Domain, RecommendationOrchestrator, MatchState (for display)
```

Read this as: an arrow means "may reference." No arrow means "must not
reference, even transitively through a project reference chain." If a
change requires adding a reference not shown here, that's the signal to
stop and reconsider the design, not to add the reference and move on.

## Rule-by-rule detail

### 1. Domain must not depend on infrastructure

**What it protects:** `MatchState`, features, and recommendation/strategy
logic (`dota-domain`) are the part of this codebase that's actually about
Dota — they should be testable with plain unit tests, no GSI server, no
screen capture, no ONNX runtime, no WPF app running. If Domain references
`Gsi.Infrastructure` or `Ui.Overlay` types, that guarantee is gone.

**How to check:** Domain project(s) should have zero project references to
`Gsi.Infrastructure`, `ComputerVision.Infrastructure`, `Ml.Inference`, or
`Ui.Overlay`. Infrastructure projects reference Domain, never the reverse.

**Common violation pattern:** a GSI payload type or a WPF-specific type
(e.g. a `System.Windows.Media.Color`) ending up as a field type on a
Domain model because it was convenient at the call site. Fix by mapping
into a Domain-owned type at the infrastructure boundary, not by letting
the infrastructure type leak through.

### 2. GSI must not contain recommendation logic

**What it protects:** `dota-gsi`'s layering — GSI answers "what does the
game currently say," never "what should the player do." If recommendation
logic ends up in GSI code, it becomes untestable without a live/fixture
GSI feed and duplicates logic that belongs in Recommendation Engines.

**How to check:** search GSI project code for anything resembling a
strategic judgment (comparisons against "good"/"bad," item scoring, hero
evaluation). If found, it belongs in the Feature Engine or a
Recommendation Engine instead — GSI should only ever be producing
normalized observations and `DomainEvents`.

### 3. Computer Vision must not contain Dota strategy

**What it protects:** the same separation as rule 2, for the other
observation source. CV's job is "pixels → structured facts," full stop.

**How to check:** CV project output types should describe observations
("item icon in slot 3 matches item ID X"), never judgments ("item in slot
3 is a good purchase"). If a CV class needs to know what makes an item
good to do its job, that's a sign strategy logic has crept into the wrong
layer.

### 4. ML must not depend on WPF/WinUI

**What it protects:** the ability to train models, run inference, and
test ML code in a CI pipeline or headless dev environment without a
Windows desktop UI stack present. This also keeps model training/
evaluation reusable outside the shipped desktop app (e.g. from a script
or notebook) if that's ever useful.

**How to check:** `Training`, `DataPlatform`, and `Ml.Inference` projects
must not reference `Ui.Overlay` or any WPF/WinUI assembly
(`PresentationFramework`, `Microsoft.WinUI`, etc.). This should be
enforceable as a straightforward project-reference check, and is a good
candidate for an automated architecture test (see "Enforcement" below).

### 5. UI must not call OpenDota (or any external API) directly

**What it protects:** a single, testable, cache-aware boundary for
external data access (the Data Platform), instead of scattered HTTP calls
from UI code that are hard to test, hard to rate-limit, and hard to keep
FairPlay-classified correctly (see `dota-fairplay` — external API data
usually lands in `PUBLIC_PREFLIGHT` or is otherwise timing-sensitive, and
that classification is easy to lose track of if calls are scattered).

**How to check:** `Ui.Overlay` project should have no reference to
whatever HTTP client / OpenDota SDK the Data Platform uses, and no direct
`HttpClient` calls to external match-data hosts. If the UI needs
historical data, it goes through the Recommendation Orchestrator/Data
Platform, pre-fetched or explicitly requested through that boundary.

### 6. Recommendation engines consume normalized features, not raw state

**What it protects:** engine logic staying decoupled from `MatchState`'s
exact shape, and staying testable against synthetic feature sets without
needing to construct a full realistic `MatchState`. It also keeps
feature-engineering logic (unit conversions, normalization, derived
metrics) from being duplicated across every engine that happens to want
a similar derived value.

**How to check:** Recommendation Engine constructors/entry points should
take a Feature Engine output type as input, not `MatchState` directly.
If an engine reaches past its declared input to read `MatchState` fields
directly (e.g. via a passed-through reference "just for this one extra
field"), that's the violation to catch in review — add the missing
feature to the Feature Engine's output instead of reaching around it.

## Enforcement

Prefer catching these mechanically over relying on review discipline
alone:

- **Project references are the first line of defense** — if the `.csproj`
  files don't reference the forbidden project, the forbidden dependency is
  impossible, not just discouraged. Structure the solution so illegal
  dependencies can't compile, per the graph above.
- **Architecture tests** — a dedicated test project asserting the
  dependency graph (e.g. using a lightweight reflection-based check that
  inspects each assembly's `ReferencedAssemblies` and fails if a
  forbidden edge exists) catches accidental violations even before human
  review, and catches transitive violations (A references B which
  references C, where A→C is forbidden) that a quick glance at one
  `.csproj` file might miss.
- **New-module checklist** — see `new-module-checklist.md`; run through
  it before adding a project so the reference graph stays correct from
  the start rather than needing correction later.

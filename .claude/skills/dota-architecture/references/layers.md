# Layers and Responsibilities

Each stage below is described as: what it owns, what it must never do,
and what it hands to the next stage. When placing new code, find the
stage whose "owns" list matches, and check the change against that
stage's "must not" list before writing it.

## Dota GSI

**Owns:** listening for GSI HTTP POSTs, parsing payloads into typed
`PlayingGameState`/`SpectatingGameState` models, diffing against prior
state, normalizing partial payloads into structured observations.

**Must not:** contain any Dota strategy or recommendation logic; decide
what's fair to expose live (that's `dota-fairplay`, enforced at the
boundary where GSI output is consumed); call external APIs; touch UI.

**Hands off:** normalized GSI observations, into the State Merger.

Full detail: `dota-gsi` skill.

## Computer Vision

**Owns:** capturing/reading the player's own screen, extracting
structured observations from pixels (HUD regions, item icons, killfeed
text, minimap state) — output should look like "field X currently reads
value Y," not a strategic judgment about that value.

**Must not:** contain Dota strategy (e.g. must not decide an observed
item is "good" or "bad" — it just reports what item icon is visible);
depend on GSI internals or vice versa (CV and GSI are sibling
observation sources, not layered on each other); read anything the
player's own screen doesn't actually show — see `dota-fairplay`'s
`VISIBLE_SCREEN` classification for the exact constraint (CV must
process the actual rendered frame, never substitute a lookalike value
from another source).

**Hands off:** normalized CV observations, into the State Merger.

## State Merger

**Owns:** reconciling GSI-sourced and CV-sourced observations into one
canonical, consistent `MatchState`. This is the one place both
observation sources meet — it resolves conflicts (e.g. GSI didn't send an
update but CV observed a change) and produces a single source of truth
for everything downstream.

**Must not:** contain strategy logic; know about ML models,
recommendation engines, or UI; reach back into GSI/CV internals beyond
consuming their normalized output.

**Hands off:** `MatchState`, into the Feature Engine.

## MatchState

Not a processing stage but the canonical data model itself — the
project's domain representation of "what is currently true about this
match," assembled by the State Merger and consumed by everything
downstream. Treat `MatchState` as domain data: it should be expressible
and testable without any infrastructure (no GSI transport types, no CV
library types, no ONNX types) leaking into its shape. See
`dependency-rules.md`'s "Domain must not depend on infrastructure" rule —
`MatchState` is the clearest example of what that rule protects.

## Feature Engine

**Owns:** transforming `MatchState` (plus static reference data — hero/
item mechanics, patch data) into normalized, structured features suitable
for both ML consumption and rule-based recommendation logic. This is
where "raw state" becomes "the inputs a recommendation actually reasons
over" — e.g. converting a raw gold value plus game minute into a
farm-efficiency feature, or raw item slots into a structured "items
currently owned" feature set.

**Must not:** make recommendations itself (it prepares inputs, it doesn't
decide outputs); depend on any specific recommendation engine's internals
(features should be engine-agnostic, not shaped around one consumer);
depend on UI.

**Hands off:** normalized features, into Recommendation Engines. This
handoff is mandatory — see `dependency-rules.md` point 6 for why
recommendation engines must not read `MatchState` directly, skipping this
stage.

## Recommendation Engines

**Owns:** turning normalized features into concrete recommendations
(item builds, timing advice, draft suggestions, etc.), using both
rule-based domain logic (`dota-domain`) and trained ONNX models where
applicable. Multiple engines can exist side by side (e.g. an item
recommender, a draft recommender) — each consumes the Feature Engine's
output, not raw state.

**Must not:** depend on WPF/WinUI (must be usable/testable headless);
call OpenDota or other external APIs directly (any needed reference data
comes pre-fetched through the Data Platform, not fetched live inline);
violate `dota-fairplay` classifications — a recommendation engine is
exactly the kind of place a `FORBIDDEN` or `SPECTATOR_ONLY` data leak
would surface, so this layer is a natural place to add FairPlay tests.

**Hands off:** recommendation objects (with reasoning/justification
fields, per `dota-domain`'s suggested output shape), into the
Recommendation Orchestrator.

## Recommendation Orchestrator

**Owns:** coordinating multiple recommendation engines, resolving
conflicts/priority between them (e.g. an item recommendation and a
timing recommendation that are both valid but the UI can only surface a
few at once), and producing the final, UI-ready recommendation set.

**Must not:** contain engine-specific strategic logic itself (that's
delegated to the engines it orchestrates); talk to GSI/CV/State Merger
directly (it consumes engine outputs, not raw state); depend on WPF/WinUI.

**Hands off:** the orchestrated recommendation set, into the UI layer.

## Overlay / Voice (UI)

**Owns:** rendering recommendations and match state to the player (WPF/
WinUI overlay, voice output), and capturing any user interaction/settings.

**Must not:** call OpenDota or other external APIs directly (rule 5);
contain recommendation logic or Dota strategy of its own; reach past the
Recommendation Orchestrator into any earlier layer.

**Consumes:** the Recommendation Orchestrator's output, plus `MatchState`
for direct display purposes (e.g. showing current gold) where that's
genuinely a display concern rather than a strategic decision.

## Historical APIs → Data Platform → Training → ONNX models (offline pipeline)

**Historical APIs** — external sources of historical match data (e.g.
OpenDota or similar). Only the Data Platform layer may call these
directly (see rule 5) — this is also where the `dota-fairplay`
`PUBLIC_PREFLIGHT`/`POSTGAME_ONLY` classifications for this kind of data
apply.

**Data Platform** — ingests, stores, and prepares historical data for
training. Owns the ETL/storage concerns; does not run live and does not
feed the live recommendation path directly (only through trained model
artifacts, never as a live passthrough — see `dota-fairplay` for why a
disguised live use of postgame/historical data is still a violation).

**Training** — produces trained models from Data Platform output. Runs
offline (batch/CI/dev-machine training runs), not as part of the live
application process.

**ONNX models** — the trained model artifacts, exported to ONNX so they
can be loaded by the live Recommendation Engines via ONNX Runtime without
requiring the training stack (Python, heavy ML frameworks) to be present
in the shipped Windows application. This is the only bridge between the
offline pipeline and the live path — a model file, not a live connection.

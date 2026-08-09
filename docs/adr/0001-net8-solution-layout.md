# ADR 0001: .NET 8 solution layout aligned with dota-architecture

## Status

Accepted

## Context

`docs/MASTER_PLAN.md` calls for a modular, local-first monorepo. The
project's own `.claude/skills/dota-architecture` skill already prescribes
a specific project graph and naming scheme for exactly this purpose,
derived from strict dependency rules (Domain has no infrastructure
references, GSI must not contain recommendation logic, UI must not call
external APIs or see GSI wire types directly, recommendation engines
consume normalized features not raw state).

## Decision

Adopt `.NET 8` (`net8.0` / `net8.0-windows` for the WPF overlay) and
create a **subset** of `dota-architecture`'s full intended layout, using
identical project names, so later phases' projects
(`ComputerVision`, `MlInference`, `DataPlatform`, `Training`,
`RecommendationOrchestrator`) can be added without renaming anything
already in place.

Created in this iteration: `FairPlay`, `Domain`, `FeatureEngine`, `Gsi`,
`RecommendationEngines`, `Host`, `UiOverlay`, plus a `tools/ReplaySimulator`
CLI and one test project per `src` project plus `Architecture.Tests`.

`Directory.Build.props` sets `net8.0`, `Nullable=enable`,
`TreatWarningsAsErrors=true`, `LangVersion=12.0` for every project.

## Consequences

- The dependency graph is enforceable and enforced
  (`DotaAiCoach.Architecture.Tests.DependencyGraphTests`), not just
  documented.
- Later phases can add projects incrementally without restructuring.
- `TreatWarningsAsErrors` means any nullable-reference warning is a build
  failure — this was chosen deliberately to keep the "every gameplay
  field is nullable, never defaulted" discipline from `MatchState`
  actually enforced by the compiler.

# ADR 0006: FeatureEngine as the mandatory seam between MatchState and rules

## Status

Accepted

## Context

`dota-architecture`'s dependency rule 6 states recommendation engines
must consume normalized features, never raw `MatchState` directly.
Without this seam, every rule would need to independently re-derive
things like "is the feed usable right now" (stale-suppression) and
"what items does the player currently own" (from raw item slots),
duplicating logic and making rules harder to unit test in isolation.

## Decision

Introduce `DotaAiCoach.FeatureEngine` as its own project with a single
responsibility: `FeatureEngine.Build(MatchState, FeedHealth,
IReadOnlyList<DomainEvent>) -> LiveFeatureSet`. `LiveFeatureSet.IsUsable`
centralizes the staleness/game-state check
(`health.Status == Live && GameState == GameInProgress`) once.
`RuleRecommendationEngine.Evaluate` short-circuits to an empty result
whenever `IsUsable` is false, so individual rules never need to check it
themselves.

## Consequences

- Rules are testable against hand-built `LiveFeatureSet` values without
  constructing a realistic `MatchState` graph.
- The stale-suppression contract (a hard requirement from `dota-gsi`) is
  enforced in exactly one place —
  `RuleRecommendationEngineTests.Returns_empty_when_features_are_not_usable_stale_suppression`
  verifies it at the engine level, not per-rule.
- `LiveFeatureSet` is the type walked by
  `DotaAiCoach.FairPlay.Tests.LiveRecommendationInputTests`'s structural
  reachability test — this seam is also what makes that FairPlay
  guarantee checkable at a single, well-defined boundary.

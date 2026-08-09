# ADR 0008: FairPlay attribute + marker interface + reflection-based structural test

## Status

Accepted

## Context

`docs/MASTER_PLAN.md` requires a `FairPlayPolicy` module from day one,
where every production feature declares source, availability,
visibility, live-usability, and justification. A purely documentary
approach (a markdown table someone has to remember to update) doesn't
prevent a future change from silently violating the policy.

## Decision

`DotaAiCoach.FairPlay` provides: `FairPlayClassification` (six values),
`DataSourceKind`, `FairPlaySourceAttribute` (carrying `Visibility`/
`Availability`/`Justification` — the five-field documentation, minus
`source`/`classification` which are the attribute's constructor
arguments), and `ILiveRecommendationInput` as a marker interface for any
type reachable as input to a live recommendation.

Every live-facing Domain/FeatureEngine type in this slice is attributed.
`DotaAiCoach.FairPlay.Tests.LiveRecommendationInputTests` walks the
reachable type graph from `RuleRecommendationEngine.Evaluate`'s actual
parameter type via reflection, failing the build if anything reachable
is unclassified or classified as anything other than a live-allowed
value — and separately confirms `RecommendationEngines` doesn't reference
`DotaAiCoach.Gsi` at the assembly level at all.

## Consequences

- This is the extension point future phases (Computer Vision, OpenDota,
  postgame replay analysis) plug into: attribute new types with their
  correct classification, and the existing structural test automatically
  extends its coverage to them without modification.
- A future type sourced from `Replay`/`ExternalApi`/`ComputerVision` that
  becomes reachable from a live rule's input fails the build immediately,
  rather than depending on a reviewer noticing during code review.
- Enums without their own `FairPlaySourceAttribute` (e.g. `GameState`,
  `ReasonCode`) are treated as pure vocabulary and skipped by the
  reachability walk — only types carrying actual player data need
  classification.

# ADR 0009: LiveMessageBudget lives in RecommendationEngines for now

## Status

Accepted (interim — see Consequences)

## Context

`dota-live-coach` mandates that live output be capped at 1-3 messages,
priority-tiered (P0/P1/P2), and rate-limited via cooldowns and
change-threshold buckets. `dota-architecture`'s full intended layout has
a separate `RecommendationOrchestrator` project responsible for exactly
this kind of cross-engine arbitration — but this iteration has only one
recommendation engine (`RuleRecommendationEngine`), so a standalone
orchestrator project would have nothing to orchestrate between yet.

## Decision

Implement the anti-spam/priority/budget logic as
`DotaAiCoach.RecommendationEngines.LiveMessageBudget` — a self-contained
class with no dependency on anything specific to `RuleRecommendationEngine`
beyond the shared `Recommendation`/`LiveMessage` Domain types. It is not
implemented inside the UI layer (`dota-architecture`: UI must not contain
recommendation logic) and not left unimplemented as a stub.

## Consequences

- When a second recommendation engine is added (e.g. a future
  ONNX-backed item scorer), a real `RecommendationOrchestrator` project
  should be created and `LiveMessageBudget` moved there wholesale — it
  was deliberately written with no coupling to `RuleRecommendationEngine`
  specifically, so that move should be mechanical (a namespace change,
  not a redesign).
- Until that move happens, `LiveMessageBudget`'s cooldown/dedup state
  (`_lastFiredByKey`, `_lastAnyMessageAt`) lives per `CoachSession`
  instance, which is correct for this slice (one engine, one session) but
  would need to become shared/coordinated state across engines once a
  real orchestrator exists.

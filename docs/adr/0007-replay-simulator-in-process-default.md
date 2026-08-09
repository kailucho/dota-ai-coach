# ADR 0007: Replay simulator defaults to in-process, HTTP mode is opt-in

## Status

Accepted

## Context

The GSI replay simulator needs to feed fixture sequences through the
ingestion pipeline both for automated tests (where determinism, no port
contention across parallel test collections, and fast execution matter)
and for manual debugging against a real running overlay (where exercising
the actual HTTP listener matters).

## Decision

`IReplayTarget` is the abstraction; two implementations exist:
`InProcessReplayTarget` (feeds fixtures directly into a `GsiPipeline`
instance, no network) and `HttpReplayTarget` (POSTs to a real running
`GsiListener` and observes results via a `GsiPipeline` reference the
listener writes into). Automated tests default to `InProcessReplayTarget`
throughout; exactly one test
(`GsiListenerHttpTests.Replaying_fixtures_over_http_matches_in_process_result_and_returns_200`)
uses `HttpReplayTarget` to prove the listener itself works end to end.

`ReplayStep.StateAfter` is produced via `MatchState.Clone()`, not a
direct reference to the pipeline's live (mutable) state — an early
version of this code returned the live reference directly, which caused
every historical `ReplayStep` in a completed sequence to silently show
the *final* state instead of that step's own state, since `MatchState` is
mutated in place. This was caught by
`ReplaySequenceTests.Laning_sequence_merges_partial_payloads_correctly_at_every_step`
asserting on an intermediate step, which is exactly the kind of bug
sequence-based (assert-after-each-step) testing exists to catch.

## Consequences

- Test suites stay fast and deterministic by default.
- The one HTTP-mode test provides real confidence the Kestrel listener
  (ADR-0002) actually works, not just the in-process pipeline.
- The `Clone()`-on-snapshot lesson is now enforced by a passing regression
  test; any future replay-target implementation must follow the same
  pattern or the existing sequence tests will catch the regression.

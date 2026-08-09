# End-to-End Tests

## What they prove

That a realistic scenario works through the *whole* live pipeline —
GSI/CV input through State Merger, MatchState, Feature Engine,
Recommendation Engines, Recommendation Orchestrator, to a
displayed/spoken recommendation — not just that each stage works in
isolation (unit tests) or that adjacent stages hand off correctly
(integration tests). End-to-end tests catch the class of bug where every
individual piece is correct but the whole doesn't behave as expected
together — a timing assumption that holds for each component alone but
not under the actual sequencing of a real match, for instance.

## How to build one for this project

The GSI replay simulator (`dota-gsi`) is the natural backbone for a live-
path end-to-end test: feed a realistic sequence of captured/fixture GSI
payloads (optionally alongside CV fixture input, if the scenario involves
computer vision) through the full pipeline, and assert on the final
recommendation output rather than on any intermediate stage.

A good end-to-end test scenario:
- covers a realistic span of a match (not just one payload) so it
  exercises the temporal/state-accumulation behavior that a single-
  payload test can't;
- includes at least one "interesting" event the recommendation pipeline
  should react to (an item purchase, a level-up, a hero death) so the
  test actually verifies reactive behavior, not just that the pipeline
  doesn't crash;
- asserts on the recommendation's content/reasoning in a way that would
  fail if a `dota-domain` strategic assumption were violated, not just
  that *some* recommendation was produced.

## What end-to-end tests are not for

They're not a substitute for the more targeted test categories — an
end-to-end test running slow and covering a lot of surface area makes it
a poor tool for pinpointing *why* something broke, and a poor tool for
exhaustively covering edge cases (that's what fixture tests, unit tests,
and FairPlay/leakage tests are for). Keep the end-to-end suite focused on
a moderate number of realistic, high-value scenarios rather than trying
to make it exhaustive — exhaustiveness belongs in the narrower test
categories where failures are fast and specific.

## Where this fits in the Definition of Done

Not every change needs a new end-to-end test — most changes are
adequately covered by the narrower categories. Add or update an
end-to-end test when a change alters overall pipeline behavior in a way
that's only observable at the full-pipeline level (e.g. a change to how
the Recommendation Orchestrator prioritizes between engines, or a new
kind of reactive behavior spanning multiple stages) — see
`definition-of-done.md` for how this is scoped per change type.

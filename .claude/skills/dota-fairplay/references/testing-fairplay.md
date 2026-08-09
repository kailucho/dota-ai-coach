# Automated FairPlay Testing

A classification written in a comment can drift from what the code
actually does — someone adds a field to a MatchState-adjacent object,
copies an existing pattern without re-checking visibility, and a
`FORBIDDEN`-adjacent value ends up feeding a live path. Automated tests
exist to catch that drift mechanically, not just rely on review
discipline.

## What every live feature's tests must assert

1. **Classification matches an allowed live category.** For anything
   feeding a live recommendation path, assert its
   `fairPlayClassification` is one of `LIVE_ALLOWED`, `VISIBLE_SCREEN`, or
   `PUBLIC_PREFLIGHT` — never `POSTGAME_ONLY`, `SPECTATOR_ONLY`, or
   `FORBIDDEN`.

2. **No FORBIDDEN or SPECTATOR_ONLY data reaches the live path at all.**
   This should be enforceable structurally where possible (e.g. a
   `FORBIDDEN`/`SPECTATOR_ONLY`-classified type that simply isn't
   constructible/reachable from the live pipeline's input types), with a
   test asserting that structural boundary holds — not just a runtime
   check that happens to pass today.

3. **POSTGAME_ONLY data never reaches a live recommendation.** Specifically
   test that a live-path recommendation, when given a partially-completed
   match, produces the same result whether or not the (not-yet-existing)
   post-game replay data is present — since it shouldn't be consulted at
   all pre-game-end. A useful test shape: construct a live recommendation
   using only pre-game-end inputs, then separately verify no code path in
   that recommendation touches replay-parsing types or postgame-only data
   sources.

## Suggested test shapes (adapt to this project's actual test framework)

Given the project references `dotnet test` (per the `dota-gsi` skill),
these translate naturally to xUnit/NUnit-style tests, but the shapes below
are framework-agnostic:

```
FairPlayClassificationTests
  - Test: every field in MatchState has a documented, valid classification
  - Test: no MatchState field is classified FORBIDDEN or SPECTATOR_ONLY
          (if one exists as FORBIDDEN, it must not be on MatchState at all —
           this test existing at all is a smell; prefer making it
           structurally impossible instead)

LiveRecommendationInputTests
  - Test: the live recommendation entry point's input type cannot be
          constructed from replay-only data types (compile-time check
          where the type system allows it; a reflection-based check
          otherwise)
  - Test: feeding a live recommendation function a MatchState with all
          "hidden" fields at their default/unknown value still produces a
          coherent (not error, not garbage) result — proves the feature
          doesn't implicitly depend on data it shouldn't have

CvFeatureVisibilityTests (for computer-vision features)
  - Test: the CV pipeline's output for a given field is null/unknown when
          the corresponding screen region isn't visible in the captured
          frame — proves it isn't silently substituting a non-screen data
          source when vision is unavailable
```

## Where to add these tests

Follow whatever test project structure already exists in the repo for
GSI/domain tests (see `dota-gsi`'s testing note: `dotnet test` before
considering GSI work complete). FairPlay tests belong alongside the
feature they cover — a new MatchState field's PR should add or extend a
classification test in the same change, not defer it.

## What "done" looks like

A live feature is not complete — regardless of how correct its
classification documentation looks — until:
- its FairPlayClassification is documented (see
  `feature-documentation-template.md`)
- an automated test asserts that classification holds
- `dotnet test` (or the project's equivalent full test run) passes with
  that test included

If writing the test reveals the classification doesn't actually hold
(e.g. the "structural" guarantee isn't really structural, or the feature
actually needs hidden data to work as designed), that's a real finding —
redesign the feature, don't weaken the test to make it pass.

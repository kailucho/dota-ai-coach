# Regression Tests for Bug Fixes

## The rule

Every bug fixed should receive a regression test when practical. The
regression test is not an optional addendum to the fix — it *is* the part
of the fix that prevents the bug from coming back. A code change without
a regression test has fixed today's symptom but left tomorrow's
recurrence just as likely, because whatever gap in test coverage let the
bug ship in the first place is still there.

## Writing a regression test that actually catches recurrence

The naive approach — writing a test for the *exact* input from the bug
report — often produces a weak regression test, because it's easy to fix
the exact reported case without fixing the underlying issue, and the next
occurrence (a slightly different input hitting the same root cause) sails
right past a too-narrow test. Instead:

1. **Identify the actual root cause**, not just the symptom. If a GSI
   payload with a missing `hero` section crashed the normalizer, the root
   cause might be "the normalizer assumes `hero` is always present," not
   "this one specific payload is broken."
2. **Write the test against the root cause**, generalizing slightly
   beyond the exact reported input where reasonable — e.g. test that the
   normalizer handles *any* payload missing an expected section
   gracefully, not just the one specific fixture that happened to
   surface the bug.
3. **Confirm the test actually fails without the fix.** Before finalizing,
   verify the regression test fails against the pre-fix code (or the
   original bug's conditions) and passes with the fix. A regression test
   that would have passed even without the fix isn't actually testing the
   bug — this is easy to get wrong when writing the test after already
   having the fix in mind.

## Placing the regression test in the right category

Use the test categories from `SKILL.md`/the other reference files based
on where the bug actually was, not just wherever is most convenient:
- A GSI parsing bug → a GSI fixture test (`gsi-testing.md`), likely added
  to the permanent fixture library so it also guards future changes.
- A merge/sequencing bug → a GSI replay simulator test.
- A FairPlay classification bug → a FairPlay test (`fairplay-and-ml-tests.md`)
  — these are especially important to add, since a FairPlay bug recurring
  is a much more serious outcome than most other bug categories.
- A leakage bug in a model's features → an ML leakage test.
- A strategic-reasoning bug (a recommendation that violates `dota-domain`
  expectations) → a unit test on the specific Recommendation Engine
  logic, ideally with an assertion that would also catch analogous
  mistakes for other heroes/items, not just the one that was reported.

## "When practical" — legitimate exceptions

Not every bug can reasonably get an automated regression test — e.g. a
bug that only manifests due to a specific external environment condition
that isn't reproducible in the test harness (a particular GPU driver
quirk affecting CV capture, a timing issue only reproducible on specific
hardware). When skipping a regression test:
- state explicitly why it's impractical, rather than silently omitting
  it;
- consider whether a narrower, still-useful test is possible even if the
  full scenario isn't reproducible (e.g. testing the code's *handling* of
  a malformed input similar in shape to what the environment condition
  produced, even without reproducing the environment condition itself);
- treat "I didn't have time" as different from "this is genuinely
  impractical to test" — the former is not a valid exception, per this
  skill's Definition of Done.

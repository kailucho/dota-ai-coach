# Leakage and Feature Availability

## The critical rule, restated precisely

> A live model may never train using information unavailable to the
> player at the equivalent game timestamp.

"Equivalent game timestamp" is the precise part: it's not enough for a
feature to be *generally* derivable from legitimate data (e.g. `dota-fairplay`'s
`LIVE_ALLOWED`/`VISIBLE_SCREEN` categories) — it must be derivable using
only information that existed *as of the specific in-game moment* the
live model will see it at inference time. A feature can be built from
100% legitimate data sources and still leak, if it's computed using data
from later in the same match than the model will actually have access to
at inference.

## Concrete leakage patterns specific to this project

**Full-match aggregates used as a per-timestamp feature.** E.g. a
"average gold per minute this game" feature computed from the full match
duration, used as an input at minute 10 — this silently includes gold
earned in minutes 11-40, which the model couldn't know at minute 10 at
inference time. Fix: compute the feature using only data up to and
including the target timestamp, every time — never using the full match
as a shortcut during training.

**Match-outcome-adjacent features.** A feature like "did the winning team
buy this item" or anything derived from `radiant_win`/final outcome,
applied to a mid-game prediction task — this leaks the answer into the
input, though sometimes not obviously (e.g. an item-timing feature
computed only from games where a team won might implicitly encode
outcome). Fix: for any live, mid-game model, exclude match-outcome and
anything derived from post-timestamp events from the feature set
entirely, not just from the direct label.

**Patch/reference data leakage.** A feature joining current match state
against "the latest patch's item costs" instead of "the item costs as of
this match's own patch" leaks future balance information into historical
training examples — this is the ML-specific case of `dota-data`'s patch/
timestamp rule. Fix: always join reference data by the match's own patch,
never the latest available patch at training time.

**Fog-of-war / replay-only data used for a live model.** Full-match
replay data legitimately reveals hidden information once a match is over
(`POSTGAME_ONLY` per `dota-fairplay`) — using that same data to train a
model that will predict live, in-progress state effectively teaches the
model to reconstruct hidden information, and it will reproduce that
"ability" at inference (even though at inference time it obviously
doesn't have the hidden data — the *training signal itself* is
contaminated, which degrades and misrepresents what the model actually
learned, aside from being a straightforward fairness violation for
anything resembling that use). Fix: for a live model, only use features
computable from `LIVE_ALLOWED`/`VISIBLE_SCREEN`/`PUBLIC_PREFLIGHT` data,
evaluated at the correct in-match timestamp — full stop, not a judgment
call to make per-feature.

**Player/account leakage between train and test.** Beyond `dota-data`'s
general train/test leakage rule, a live *behavioral* model (e.g.
predicting a specific player's tendencies) needs extra care: if the same
player's matches appear on both sides of a split and the model is meant
to generalize to unseen players, that's leakage specific to this task,
even if the temporal split is otherwise correct.

## How to test for it, not just reason about it

Reasoning through the list above is necessary but not sufficient — write
automated leakage tests as a required part of every model's test suite:

1. **Timestamp-bound feature computation test.** For any feature meant
   to be available at time T, assert that computing it using only data
   up to T produces the same value as computing it with the full match
   available — if these differ, the feature is leaking future data
   through an implementation shortcut (e.g. accidentally using a
   full-match dataframe instead of a truncated one).
2. **FairPlay classification test.** Reuse `dota-fairplay`'s testing
   approach: assert every feature used by a live model has a documented
   classification of `LIVE_ALLOWED`, `VISIBLE_SCREEN`, or
   `PUBLIC_PREFLIGHT` — never `POSTGAME_ONLY`, `SPECTATOR_ONLY`, or
   `FORBIDDEN`. This should be a structural/mechanical test, not a
   manual review checklist item, per `dota-fairplay`'s own testing
   guidance.
3. **Patch-pinning test.** For any feature joining static reference data
   (item costs, hero stats), assert the join uses the match's own patch
   field, not "current"/"latest" patch — a unit test constructing a
   historical match against an intentionally-different "current" patch in
   the test environment will catch an accidental latest-patch join
   immediately.
4. **Ablation sanity check.** As a softer signal (not a substitute for
   the tests above): if removing a single feature causes a suspiciously
   large drop in validation performance, especially one out of proportion
   to what the feature should strategically matter (per `dota-domain`),
   investigate whether that feature is leaking rather than just assuming
   it's simply "very predictive."

## Feature availability validation as a standing practice

Every feature going into a live model's feature set should carry the same
five-field documentation `dota-fairplay` requires for live features
(`source`, `availability`, `visibility`, `fairPlayClassification`,
`justification`) — treat the ML feature pipeline as a live feature
producer subject to that same discipline, not a separate concern. If a
feature can't be described that concretely, that's a sign it might not
survive a feature-availability audit, and it shouldn't go into a live
model's training data until it can.

# Coordinating with dota-fairplay

## Why CV needs this coordination more than most components

Per `dota-fairplay`, `VISIBLE_SCREEN` is defined by a strict constraint:
data must come from the player's *actual rendered frame*, captured and
processed faithfully, never substituted or reconstructed from another
source even if the output would look identical. CV is the *only* system
component whose entire purpose is producing `VISIBLE_SCREEN` data — which
means CV bugs are disproportionately likely to be fair-play bugs, not
just correctness bugs. A misrecognition in, say, an OCR digit read is a
quality bug; a CV feature that quietly stops requiring visibility and
starts reporting stale/inferred data is a fair-play bug wearing a CV
bug's clothing, and the two can look identical from a quick glance at the
output.

## The specific failure mode this skill exists to prevent: "no hidden information inference"

CV's job stops at "what does this frame show." Any temptation to have CV
(or a component immediately downstream of it, without a clear layer
boundary) *infer* something not actually shown — an enemy's likely
inventory from gold/time heuristics, a probable minimap position from a
last-known location plus elapsed time, anything of that shape — is out of
scope for CV entirely, regardless of how useful or "probably right" the
inference would be. This isn't a judgment call to make per-feature: if a
proposed CV feature's actual mechanism is "recognize X, then estimate Y
from X," the Y part does not belong in CV, and more importantly, should
be examined under `dota-fairplay`'s full classification system before
being built anywhere in the system — most such inferences will land on
`FORBIDDEN` once actually classified (see `dota-fairplay`'s worked
example on exactly this shape of feature, in
`feature-documentation-template.md`).

## Required coordination points

**Before building a new CV recognition target:**
1. Identify which `references/allowed-sources.md` category it falls
   under, and confirm the visibility condition is well-defined and
   checkable (per that file's "the general pattern across all sources").
2. Draft the FairPlay documentation (`source`/`availability`/`visibility`/
   `fairPlayClassification`/`justification`, per `dota-fairplay`'s
   template) *before* implementation, not after — if `visibility` can't be
   stated concretely at design time, that's a signal to reconsider the
   feature before writing recognition code for it.
3. Confirm `fairPlayClassification` is `VISIBLE_SCREEN`. If it isn't,
   stop and escalate rather than building — a CV feature that doesn't
   land on `VISIBLE_SCREEN` is architecturally out of place regardless of
   how it's implemented.

**While implementing:**
- Confirm the confidence/temporal-smoothing implementation respects the
  boundary in `confidence-and-smoothing.md` — smoothing must never
  extend a value's lifetime past actual visibility loss.
- Confirm no downstream fallback silently substitutes a non-frame-sourced
  value when capture/recognition fails — failure must propagate as "no
  observation," never as a plausible-looking guess.

**Before shipping:**
- A FairPlay test (per `dota-testing`'s CV-testing and FairPlay-testing
  guidance) exists asserting the feature reports "unavailable" when its
  source region isn't visible, not a stale or substituted value.
- The FairPlay documentation from step 2 above is finalized and matches
  what was actually built (re-verify — implementation details sometimes
  drift from the original design in ways that change the classification
  reasoning, e.g. a smoothing window turning out looser than intended).

## When in doubt

Per `dota-fairplay`'s own guidance: default toward the restrictive
classification and escalate rather than guessing permissively. This
applies with extra weight to CV specifically, since CV's entire
legitimate purpose is bounded by visibility in a way most other system
components' inputs aren't — there's very little room for a "probably
fine" judgment call here that isn't actually available in components
governed primarily by `LIVE_ALLOWED`/`PUBLIC_PREFLIGHT` data.

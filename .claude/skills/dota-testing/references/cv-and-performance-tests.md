# Computer Vision Tests and Performance Benchmarks

## CV tests

**What they prove:** the computer-vision pipeline correctly extracts
structured observations from known screen captures, and — just as
importantly — correctly reports "unavailable" rather than guessing or
substituting a plausible-looking default when the relevant screen region
isn't actually visible in the captured frame.

**Why the "unavailable" case matters as much as the "correct extraction"
case:** per `dota-architecture`'s CV layer rules and `dota-fairplay`'s
`VISIBLE_SCREEN` classification, a CV feature is only legitimate when it
reflects what's actually rendered on the player's own screen at that
moment. A CV component that silently falls back to a cached/estimated
value when the region isn't visible has quietly turned a `VISIBLE_SCREEN`
feature into something else — potentially a FairPlay violation, and
always a correctness bug. Test both directions explicitly: correct
extraction when the region is visible, and correct "no data" reporting
when it isn't.

**What makes a good CV test:** a library of known input screen captures
(or crops of realistic HUD regions) with hand-verified expected outputs,
covering:
- normal, clearly-visible cases (the straightforward positive case);
- partially-occluded or edge-of-visibility cases (the boundary the
  "unavailable" reporting needs to handle correctly);
- visually similar-but-wrong cases (e.g. two items with similar icons)
  to catch misclassification, not just total failures.

Keep these tests deterministic — CV tests should run against static
captured images, not a live game session, so they're reliable in CI and
don't depend on being able to reproduce a specific live game state.

## Performance benchmarks

**What they prove:** live-path code — GSI ingestion, CV extraction, model
inference, the Recommendation Orchestrator — stays within a latency
budget appropriate for a real-time overlay, and that budget doesn't
silently regress as the codebase grows.

**Why this is its own category, not just "part of testing":** a
correctness test can pass while a change makes the live path too slow to
actually be useful in a real match — a recommendation that's correct but
arrives three seconds late is not a working feature for this product.
Performance is a first-class requirement here specifically because the
whole point of the live path is to be useful *during* a match, not after
it.

**What to benchmark:**
- CV extraction time per frame (must fit comfortably within the
  overlay's intended refresh cadence).
- GSI payload parsing/normalization time (should be negligible relative
  to GSI's own update cadence, but verify rather than assume as payload
  complexity grows).
- Model inference latency (per `dota-ml`'s ONNX export guidance — this is
  explicitly part of that skill's export validation checklist too;
  don't treat it as covered just because export validation checked it
  once, since latency can regress with later model versions).
- End-to-end latency from a state change arriving to a recommendation
  being ready to display, as the metric that actually matters to the
  player's experience, in addition to the per-component numbers above.

**How to prevent silent regression:** record benchmark results over time
(not just pass/fail against a fixed threshold) so a gradual creep is
visible even if no single change crosses the threshold on its own. A
threshold-only benchmark catches a sudden regression but not a slow
accumulation of small ones across many changes.

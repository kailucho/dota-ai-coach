# Confidence Thresholds and Temporal Smoothing

## Why confidence thresholds are required, not optional

A recognition result without a confidence check is a guess presented as
a fact. Given how many things can degrade recognition quality in this
project specifically — resolution/scaling mismatches
(`resolution-and-scaling.md`), partial occlusion, unusual UI themes/
skins, capture artifacts — a recognizer will sometimes produce a
low-quality, low-confidence result even when the pipeline as a whole is
working correctly. The confidence threshold is what stops that
low-quality result from silently reaching `ObservableState` and, from
there, a Recommendation Engine that has no way to know the input was
shaky.

## Setting thresholds per recognition target

Don't use one global confidence threshold for every recognition target —
different targets have different cost profiles for a false
positive/negative:

- A **high-stakes** target (e.g. misreading an enemy's item as something
  it isn't, which could feed a wrong counter-item recommendation)
  deserves a stricter threshold — better to report "unavailable" than a
  wrong confident-looking value.
- A **low-stakes, easily-corrected** target (e.g. a kill-feed entry that
  will simply be superseded by the next entry) can tolerate a somewhat
  looser threshold, since an occasional miss has limited downstream
  consequence.

Set and document each target's threshold with this reasoning explicit,
not as an arbitrary tuned constant — this also makes the threshold
something a future change can deliberately revisit rather than silently
inherit without understanding why it was set where it was.

## What happens below threshold

A recognition result below its confidence threshold must be treated as
**no observation**, not as a low-confidence observation passed through
anyway. `ObservableState` should represent "not currently
observed/recognized with sufficient confidence" as a distinct, explicit
state (per `pipeline.md`'s `ObservableState` notes) — never silently
omitted (which risks a downstream consumer misinterpreting missing data
as "unchanged from last observation," which is a distinct and also
dangerous bug) and never passed through with a caveat that a
downstream consumer might ignore.

## Temporal smoothing

**Purpose:** reduce frame-to-frame recognition noise by combining
results across multiple frames before committing to a stable observed
value — e.g. requiring N consecutive frames to agree (or a weighted
majority within a sliding window) before updating `ObservableState`,
rather than trusting any single frame's result outright.

**The critical constraint, repeated from `pipeline.md` because it's the
easiest place to introduce a fair-play bug:** smoothing must reduce noise
*while a source remains visible* — it must never extend a value's
reported lifetime *past the point the source stops being visible*. A
smoothing window that holds onto the last confidently-recognized value
for some duration after the underlying region is no longer rendering
that content has turned a legitimate `VISIBLE_SCREEN` feature into a
`FORBIDDEN` one, because it's now reporting information the player can no
longer actually see.

**How to implement this correctly:**
- Track "source currently visible" as a separate signal from "recognized
  value," and let visibility loss immediately clear/invalidate the
  smoothed value — don't let the smoothing window's internal buffer
  simply age out on its own timer independent of visibility.
- When a source becomes visible again after an absence, don't
  pre-seed the smoothing window with the previous value — start fresh,
  even if the previous and new values turn out to be the same. The
  *correctness* of starting fresh matters more than the minor latency
  cost of re-establishing confidence, given what's at stake if this
  boundary is gotten wrong.
- Distinguish "briefly occluded within an otherwise continuous
  visibility period" (where smoothing across a short gap is reasonable —
  e.g. a HUD element flickered behind a particle effect for one frame)
  from "source genuinely left visibility" (where it isn't) — this
  distinction should be based on the *actual visibility signal*, not just
  "did recognition confidence drop for a frame," since those are
  different things and conflating them risks either flickering
  (over-strict) or the fair-play-violating stale-hold (over-lenient).

## Testing implication

Per `dota-testing`'s CV-testing guidance, test both directions
explicitly: smoothing correctly reduces noise across a sequence of
frames where the source stays visible and the true value is stable, and
smoothing correctly and promptly clears the observed value once a
sequence shows the source becoming not-visible — treat the second case as
at least as important to test as the first, since it's the one with
fair-play consequences if it regresses.

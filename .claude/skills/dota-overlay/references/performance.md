# Performance: Low CPU/GPU Usage

## Why this is a hard requirement here specifically

The overlay renders continuously, concurrently with the game, competing
for the same GPU/CPU resources Dota itself needs to maintain frame rate.
Unlike a typical desktop app where a few percent of extra CPU/GPU usage
is invisible to the user, here it directly costs the player frame rate
during competitive play — and it costs it disproportionately during the
exact moments (team fights, high-pressure decisions) where the game
itself is already GPU-intensive and the player's tolerance for
degradation is lowest. A "small" overlay overhead is not small in this
context.

## Concrete guidance

**Render only on actual content change.** The overlay should not
continuously redraw/re-render components whose content hasn't changed —
tie rendering to actual state updates (a new recommendation, a priority
change, a `dota-live-coach`-gated message firing) rather than a fixed
render-loop tick that redraws regardless of whether anything changed.
This is a direct consequence of `dota-live-coach`'s anti-spam mechanisms
(hysteresis/cooldowns/thresholds) already bounding how often content
actually changes — the overlay's rendering cost should track that same
bounded update rate, not run independently at a higher fixed frequency.

**Avoid unnecessary continuous animation.** Constant motion (looping
animations, continuous transitions, animated backgrounds) costs ongoing
GPU cycles for purely cosmetic benefit, working directly against this
skill's performance requirement. Prefer:
- one-shot transitions on actual state change (a card appearing/
  disappearing/updating), not continuous idle animation;
- static rendering during any period where content isn't changing, which
  should be the overlay's normal resting state given how infrequently
  `dota-live-coach`'s presentation rules allow new content to appear.

If motion is used for a specific, valuable purpose (e.g. drawing
attention to a new P0 alert, per `dota-live-coach`'s priority tiers), keep
it brief and bounded (a short, finite animation on the transition itself)
rather than a continuous/looping effect.

**Minimize overdraw.** Keep the transparent regions of the overlay window
genuinely cheap to composite — avoid layering multiple semi-transparent
elements unnecessarily, and keep the actual rendered/opaque area limited
to what components are currently showing (consistent with the
minimal-UI/compact-mode preference in `SKILL.md`, which is also a
performance win, not just a UX one).

**Profile against the actual constraint, not a generic desktop-app
budget.** "Low" here means low relative to a budget that also has to
leave headroom for the game itself — validate actual CPU/GPU cost while
Dota is running under representative load (e.g. during a team fight,
not just at the main menu), not just in isolation with no game running.
A profiling result that only measures the overlay in isolation
understates its real-world cost, since the interesting failure mode is
specifically about competing with the game for the same resources.

## Testing implication

Per `dota-testing`'s performance-benchmark guidance, overlay rendering
cost is a first-class benchmark target, tracked over time (not just
pass/fail against a fixed threshold) so gradual regressions across many
small changes remain visible. Benchmark under representative load
(alongside a running/simulated match, not the overlay alone), and
include both CPU and GPU measurements, since a change can regress one
without regressing the other (e.g. a rendering change that adds GPU
compositing cost without meaningfully increasing CPU usage would be
missed by a CPU-only benchmark).

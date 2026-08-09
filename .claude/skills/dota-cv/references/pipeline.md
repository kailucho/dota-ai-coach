# CV Pipeline, Stage by Stage

## Windows Graphics Capture

**Job:** capture the actual rendered frame from the game window, using
the Windows.Graphics.Capture API (the modern, supported Windows screen/
window capture mechanism — prefer this over older approaches like GDI
BitBlt or duplicated-desktop capture unless there's a specific
compatibility reason not to, since Graphics Capture is what Windows
itself recommends for this use case and integrates cleanly with a .NET
desktop app per `dota-architecture`).

**Constraint:** this is the *only* legitimate entry point to the whole
CV pipeline. Every downstream value must trace back to an actual captured
frame from this stage — never a substituted, cached-across-capture-loss,
or externally-sourced "equivalent" value. If capture is temporarily
unavailable (window minimized, capture permission issue, etc.), that must
propagate downstream as "no data," not silently fall back to something
else — see `dota-fairplay`'s worked example on exactly this failure mode
for `VISIBLE_SCREEN` data.

**Practical notes:** capture the specific game window, not the full
desktop, where feasible — this reduces the surface area for accidentally
capturing unrelated content (other windows, notifications) that has
nothing to do with the game and shouldn't be processed or stored at all.

## ROI (Region of Interest)

**Job:** crop the captured frame down to the specific screen regions each
recognition target needs, using known UI geometry (per `references/allowed-sources.md`'s
source list).

**Constraint:** ROI extraction is the stage most sensitive to resolution/
aspect-ratio/DPI/UI-scaling differences — see
`references/resolution-and-scaling.md`. A hardcoded pixel-coordinate ROI
that assumes one specific resolution will silently crop the wrong region
(or nothing meaningful) for any player with a different setup, and this
failure is often silent (recognition just produces garbage or
low-confidence noise) rather than an obvious crash — which is exactly why
the confidence-threshold rule (`confidence-and-smoothing.md`) matters as
a safety net here.

## Preprocessing

**Job:** normalize a cropped region for reliable recognition — typical
steps include scaling to a canonical size, color/contrast normalization,
and any denoising appropriate to the capture quality.

**Why this carries more weight here than in a typical CV pipeline:**
because ROI geometry varies by player setup (resolution/scaling/UI
options), preprocessing is what absorbs that variance before it reaches
recognition — a well-designed preprocessing stage is what lets a single
recognition model/classifier work correctly across many different player
configurations without needing per-configuration retraining. Treat
preprocessing robustness as a first-class design concern, not a
throwaway normalization step.

## Recognition

**Job:** extract the actual structured value from a preprocessed region
— which hero portrait is this, what number does this HUD text show, which
item icon is in this slot.

**Approach guidance:** see `references/recognition-approach.md` for when
to use fixed-region classification (the default, per the project's
stated preference) vs. when heavier detection/OCR approaches are actually
warranted.

**Constraint:** recognition output should be the raw structured
recognition result plus its own confidence — don't let recognition logic
apply strategic judgment about the *meaning* of what it recognized (that
would violate `dota-architecture`'s "Computer Vision must not contain
Dota strategy" rule and this skill's "no hidden information inference"
rule) — recognition answers "what icon/text/portrait is this," never "is
this good."

## Confidence

**Job:** every recognition result carries an explicit confidence score,
reflecting how certain the recognition stage is about its own output.

**Constraint:** see `references/confidence-and-smoothing.md` for how
thresholds gate what reaches `ObservableState` — a low-confidence
recognition result must not silently pass through as if it were
equally trustworthy as a high-confidence one.

## Temporal smoothing

**Job:** combine recognition results across multiple frames to reduce
frame-to-frame jitter/noise before producing a stable observation (e.g.
requiring a consistent read across several consecutive frames before
committing to a value, rather than trusting any single frame's
potentially-noisy recognition).

**Constraint — the sharpest one in the whole pipeline:** smoothing must
never extend a value's reported lifetime *past the point where the
underlying source is still actually visible*. See
`references/allowed-sources.md`'s minimap and inventory notes for exactly
why this matters: smoothing is meant to reduce noise *while* a source is
visible, not to bridge gaps *after* a source stops being visible. Get
this backwards and a well-intentioned noise-reduction feature becomes a
fair-play violation. See `confidence-and-smoothing.md` for concrete
smoothing-window guidance that respects this boundary.

## ObservableState

**Job:** the pipeline's final output — structured, confidence-scored
observations in the shape `dota-architecture`'s State Merger expects as
CV's contribution to `MatchState`.

**Constraint:** `ObservableState` should carry confidence alongside each
value (not just a bare value), and should represent "not currently
observed" as a distinct, explicit state — never silently omitted (which a
consumer might misread as "unchanged from last time") and never
backfilled with a guess. This is the CV pipeline's half of the State
Merger contract described in `dota-architecture`'s `layers.md`.

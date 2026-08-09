# Recognition Approach: Fixed-Region Classification vs. Detection

## The default: fixed-region classification, when geometry is known

Dota's UI has fixed, known layouts for most of the sources in
`allowed-sources.md` — the HUD, portrait rows, scoreboard, kill feed.
When the geometry is known (i.e. "this specific screen region always
contains this specific kind of content when visible"), prefer:

- **Crop the known region** (per `pipeline.md`'s ROI stage, with correct
  resolution/scaling handling per `resolution-and-scaling.md`).
- **Classify against a small, well-defined set of possibilities** — e.g.
  "which of these N hero portraits is this" or "which of these M item
  icons is this" — rather than running general-purpose object detection
  across the whole frame to *find* the content first.

**Why this is preferred over heavyweight detection:** when you already
know *where* something is (fixed UI geometry) and *what set of things it
could be* (a finite, known icon/portrait library), classification is
faster, more reliable, easier to validate exhaustively (test against every
possible value in the known set), and easier to debug when it's wrong
(a misclassification within a small known set is easier to diagnose than
a detection miss/false-positive in an open-ended search). It's also
cheaper at inference time, which matters directly for this project's live-
overlay performance budget (per `dota-testing`'s performance-benchmark
guidance).

## When detection (or another heavier approach) is actually warranted

Not every CV target has fixed geometry. The clearest case in this
skill's own source list: **visible minimap state**. Units on the minimap
move continuously — there's no fixed region containing "the position of
this specific hero," because the whole point is tracking where visible
units currently are. This genuinely needs a detection-style approach
(finding and classifying unit markers within the minimap region, which
itself *is* a fixed region even though its contents move).

General guidance for recognizing when detection is warranted instead of
fixed-region classification:
- the target's *position* within its containing region varies (not just
  its *content*) — minimap units are the clear example;
- the target's presence/count is variable (zero to many instances,
  rather than one fixed slot per target);
- the containing region's layout itself isn't fixed by the game's UI
  (rare in this project's source list, but worth checking rather than
  assuming fixed-region always applies).

Don't reach for detection by default just because it's more general —
per the project's stated preference, justify the heavier approach by the
target's actual geometry, the same way `dota-ml` requires justifying a
complex model over a simpler baseline. A detection-based approach for a
genuinely fixed-region target (e.g. running object detection to find the
top hero bar's portraits, which are always in the same place) is
unjustified complexity that costs more at inference time for no
corresponding benefit.

## OCR as a recognition approach

For HUD elements showing numeric/text values (gold, cooldown timers,
scoreboard numbers), OCR (optical character recognition) is often the
right recognition technique within a fixed-region-classification
framework — the region is fixed, but the specific rendered text/number
varies continuously, so classification against a fixed small set doesn't
apply the same way it does for portraits/icons. Treat OCR as the
recognition-stage technique for these specific fixed regions, not as a
detection-style departure from the fixed-region-first principle — the
ROI is still fixed and known, only the recognition technique within it
differs from icon classification.

## Validating a new recognition target's approach choice

Before implementing a new recognition target, briefly state:
- is this target's geometry fixed (position and containing region known
  and stable) or variable?
- is the target's content classification against a small known set, OCR
  against a fixed region, or genuinely open-ended detection?

If the answer is "fixed geometry, small known set" (true for most of
this skill's source list) and the implementation reaches for a
heavyweight detection model anyway, that's worth a second look before
merging — per the project's explicit preference, and consistent with
`dota-ml`'s general complexity-must-be-justified stance applied to CV.

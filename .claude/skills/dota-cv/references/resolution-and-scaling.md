# Resolution, Aspect Ratio, DPI, and UI Scaling

## Why this is a correctness issue, not a polish issue

Dota's UI is laid out in the game's own coordinate space, and Dota
supports multiple resolutions, aspect ratios, and in-game UI scaling
settings — plus Windows' own display scaling (DPI) sits on top of
whatever the game renders. A ROI region defined as fixed pixel
coordinates captured from one specific setup will silently crop the
wrong content (or content that's shifted/scaled relative to what
recognition expects) for any player running a different configuration.
This isn't a rare edge case — different monitor resolutions, aspect
ratios, and Windows scaling settings are all common and expected across
real players' machines.

## The four variables and how they interact

- **Resolution** (e.g. 1920×1080 vs 2560×1440 vs 3840×2160) — the game's
  actual rendered pixel dimensions. UI elements scale with resolution,
  but not always linearly or identically across all elements.
- **Aspect ratio** (16:9, 21:9 ultrawide, 4:3, etc.) — changes not just
  scale but *layout* — some UI elements are anchored to screen edges/
  corners, so a wider aspect ratio doesn't just stretch existing content,
  it can change where elements sit relative to each other.
- **DPI** (Windows display scaling, e.g. 100%/125%/150%) — affects how
  Windows Graphics Capture's reported frame dimensions relate to the
  game's actual rendered resolution; a naive assumption that "captured
  pixels == game's internal resolution" can be wrong under non-100% DPI
  scaling.
- **UI scaling** — Dota's own in-game HUD scale setting, independent of
  both resolution and Windows DPI, which players commonly adjust for
  readability/preference.

These compound — the same nominal resolution can produce different
actual HUD layouts depending on aspect ratio and in-game UI scale
settings alone, before Windows DPI is even considered.

## How to handle this correctly

- **Derive ROI regions relative to the actual captured frame's
  dimensions and known anchor points**, not fixed absolute pixel
  coordinates. Where Dota's HUD anchors elements to screen
  edges/corners, compute ROI positions as a function of the captured
  frame's actual width/height (and known anchor offsets), not a single
  hardcoded coordinate set tuned for one resolution.
- **Detect or read the game's actual resolution/aspect ratio/UI scale
  setting where possible** (e.g. from game configuration files or
  observable frame properties) rather than assuming a default — treating
  the most common setup as universal will produce silent failures for a
  meaningful fraction of real players.
- **Validate ROI correctness empirically across multiple configurations**
  before shipping a new recognition target — a fixed-region ROI that's
  only ever been tested at one resolution/aspect-ratio/scale combination
  has not actually been validated for this rule, regardless of how
  confident the geometry looks on the developer's own machine.
- **Prefer proportional/relative geometry definitions in code** (e.g. "20%
  from the left edge, 5% from the top" style anchoring) over absolute
  pixel constants, so a single geometry definition naturally adapts
  across resolutions with the same aspect ratio, and flag the smaller set
  of cases that need aspect-ratio-specific handling explicitly rather
  than trying to force one formula to cover every case silently.

## Testing implication

Per `dota-testing`'s CV-testing guidance, the known-capture test library
should explicitly include captures at multiple resolutions/aspect
ratios/UI-scale settings, not just one canonical configuration — a CV
test suite that only validates one setup provides much weaker assurance
than the resolution/DPI/scaling rule requires, even if every test in it
passes.

## Failure mode to watch for

A ROI/preprocessing bug caused by an untested resolution/scaling
combination typically doesn't crash — it produces plausible-looking but
wrong recognition input, which the confidence stage may or may not catch
depending on how the recognizer responds to slightly-off input (a
classifier fed a badly-cropped region can sometimes still produce a
confident-but-wrong answer, not just a low-confidence one). This is
exactly why resolution/scaling handling needs to be gotten right at the
ROI stage itself, rather than relied upon to be caught downstream by the
confidence threshold — confidence thresholds are a safety net, not a
substitute for correct geometry.

# Privacy and Storage: No Screenshots Stored by Default

## Why this rule exists

Screen capture, by its nature, has access to more than just the game —
if capture ever picks up other windows, notifications, or overlapping
content, storing raw frames creates a privacy exposure well beyond this
project's actual purpose (per "Allowed purpose" in `SKILL.md`). Even
scoped strictly to the game window, persisted frames are a form of data
this project has no legitimate ongoing need for once a frame has been
processed into a recognition result — keeping them around is pure
downside (storage, privacy surface, potential for accidental
mishandling) with no corresponding benefit to the live coaching purpose.

## The default: transient processing only

Captured frames and ROI crops are processing input for the pipeline
(`pipeline.md`), not a persisted artifact. By default:
- a captured frame exists in memory only for as long as it takes to run
  through ROI extraction, preprocessing, and recognition;
- ROI crops similarly are transient intermediate data, not written to
  disk;
- nothing from the capture pipeline is written to persistent storage
  unless one of the narrow, explicit exceptions below applies.

## Narrow, explicit exceptions

**Debugging a misrecognition.** When a recognizer produces a wrong or
unexpectedly low-confidence result and a developer needs to understand
why, saving the specific frame/ROI that triggered the issue is
legitimate — but only:
- as an explicit, deliberate action (a debug flag/mode the developer
  turns on, not an always-on background capture), never silently
  happening by default;
- scoped to the minimum necessary (the specific ROI, not the full
  captured frame, where the ROI alone is sufficient to diagnose the
  issue);
- clearly communicated to whoever is running the application if this
  debug capability is ever exposed to end users (e.g. a "report a
  misrecognition" feature) — the player should know explicitly when a
  frame is being saved and why, not discover it after the fact.

**Building the known-capture test library** (per `dota-testing`'s CV
test guidance). Test fixtures are a legitimate, deliberate exception —
but these should be sourced from controlled capture sessions explicitly
set up for building the test library (ideally synthetic/mocked-up HUD
captures where feasible, or captures from a consenting
developer/tester's own session), never silently accumulated from a real
user's live coaching session without their explicit awareness and
consent.

## What "by default" means in practice

If a CV feature or debug tool needs to persist any captured
frame/ROI/recognition-input data beyond the narrow exceptions above, that
need should be a deliberate, reviewed design decision — not an
incidental side effect of how a feature happens to be implemented (e.g.
"we logged the full frame because it was convenient for the log
statement" is not an acceptable justification). Default to discarding
processing input the moment it's no longer needed for the current
frame's recognition pipeline.

## Relationship to dota-fairplay

This rule is about privacy/storage hygiene, distinct from (though
related to) `dota-fairplay`'s classification system — a stored screenshot
of legitimately `VISIBLE_SCREEN` content isn't automatically a fair-play
violation in the way a `FORBIDDEN` data source would be, but it's still a
real privacy/data-hygiene concern this project should avoid by default,
for the reasons above. Treat the two concerns as separate but
complementary: FairPlay classification governs *what CV may observe and
use*; this rule governs *what CV may persist* once it has.

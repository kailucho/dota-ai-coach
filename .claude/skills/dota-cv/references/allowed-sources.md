# Allowed Sources, In Detail

Each source below is legitimate specifically because of *when* and
*under what condition* it's actually rendered — that condition is part of
the source's definition, not a footnote. A recognizer that reads the
right region but ignores the condition under which that region is
meaningful can silently produce `FORBIDDEN`-equivalent output even while
technically only reading pixels.

## Draft portraits

Rendered during the draft/pick-ban phase, showing picks (and bans, in
modes with a formal ban phase) as they're locked in. This is public,
game-design-intentional information once shown — both teams see the same
draft UI. Straightforward `VISIBLE_SCREEN` (arguably overlapping with
`PUBLIC_PREFLIGHT` once the draft is complete and picks are locked, per
`dota-fairplay`) — no special visibility condition beyond "the draft UI
is currently showing this."

## Top hero bar

The row of hero portraits/status icons shown throughout the match.
**Careful boundary:** this bar can show *some* status information about
heroes regardless of current vision on that specific hero (e.g. whether a
hero is dead/alive, which is intentionally shown to all players by game
design) — that's legitimately `VISIBLE_SCREEN`/`LIVE_ALLOWED`-equivalent,
since it's always rendered. But don't extend a recognizer built for this
bar to infer anything *beyond* what it actually displays (e.g. don't
pair alive/dead status with an inference about the dead hero's likely
respawn-adjacent position) — recognize exactly what the bar shows, no
more.

## Selected unit HUD

Stats/abilities/items for whatever unit is currently selected/inspected
by the player. Legitimate when and only when the player has actually
selected/is inspecting that unit — this includes inspecting an enemy
hero when game rules permit it (e.g. during a fight where the enemy is
visible and selectable). The condition "currently selected and rendering"
is what makes this `VISIBLE_SCREEN` — a recognizer must not cache the
last-seen values and continue reporting them once the selection changes
away or the unit is no longer visible/selectable.

## Visible inventory

Item slots for a hero currently shown on screen — the player's own
(always available) or an enemy's (only when their portrait/HUD is
actually rendered, per "selected unit HUD" above, or via the top hero
bar if the game UI shows it there). This is `dota-fairplay`'s
`VISIBLE_SCREEN`-classification worked example almost verbatim — see
that skill's `feature-documentation-template.md` for the exact reasoning.
**The recognizer must stop reporting an enemy's items the moment they're
no longer visible** — it must not continue reporting the last-observed
inventory as if it were current, since Dota items can change (used
consumables, new purchases) and a stale report misrepresents current
state as confidently as a fabricated one would.

## Scoreboard

The tab/score-panel view, only while actually open. This is a genuinely
intermittent source — the scoreboard isn't always rendered, so a
recognizer targeting it must correctly report "not currently available"
the rest of the time rather than holding onto stale data (see
`confidence-and-smoothing.md` for how temporal smoothing must not paper
over a source going away).

## Kill feed

The on-screen kill notification log — transient, time-limited entries.
Legitimate to read while displayed; a recognizer should treat each
kill-feed entry as a point-in-time event observation (feeding
`DomainEvents`, per `dota-gsi`'s architecture) rather than persistent
state, since the feed itself is transient by game design.

## Visible minimap state

Only what the minimap actually renders — visible allied/enemy units,
visible wards, visible objectives-in-progress. **This is the source with
the sharpest fair-play edge in the whole list**: the minimap's rendering
already respects fog-of-war by game design (it doesn't show hidden
units), so a correct minimap recognizer is automatically FairPlay-
compliant *as long as it only reads what's rendered*. The failure mode to
guard against specifically: any temporal-smoothing or tracking logic
(see `confidence-and-smoothing.md`) that "remembers" a unit's last-seen
minimap position and continues reporting it after the unit leaves vision
— that's exactly the `FORBIDDEN` fog-of-war-piercing behavior
`dota-fairplay` prohibits, introduced not by reading the wrong region but
by over-eager smoothing across a source that legitimately went away. Test
this boundary explicitly (per `dota-testing`'s CV-testing guidance).

## The general pattern across all sources

For every source above, "is this visible" is not a static yes/no about
the *region* — it's a per-frame condition about whether the underlying
game state is actually being rendered right now. CV logic must check that
condition every frame, not assume that because a region *can* show useful
data, it currently *is*. Getting this wrong is the most likely way a
technically-correct-looking CV feature becomes a fair-play violation.

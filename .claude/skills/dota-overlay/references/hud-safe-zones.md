# HUD Safe Zones and Draggable Configuration

## What counts as "essential Dota HUD area"

The regions the player needs unobstructed to actually play the game —
these are the same fixed-layout regions `dota-cv`'s `allowed-sources.md`
already documents from the *reading* side; here they matter from the
*drawing* side, since covering them with an overlay component is exactly
as harmful to the player as it would be uninformative to a CV
recognizer:

- **Minimap** — constant situational awareness; covering it is one of
  the most immediately disruptive mistakes an overlay can make.
- **Inventory / item slots** — needed for moment-to-moment item usage.
- **Ability bar** — needed for moment-to-moment ability usage.
- **Health/mana bars** (own hero and, during fights, visible
  allies/enemies) — needed for life-or-death decisions in real time.
- **Top hero bar** — ongoing match-state awareness.
- **Selected-unit HUD area** — whatever the player is currently
  inspecting.
- **Central play area** — the region where actual gameplay (hero
  movement, combat) is happening; even outside the fixed HUD chrome, a
  component that renders over the middle of the screen during a fight is
  a serious problem regardless of whether it's technically "HUD."

## The rule in practice

**Default overlay component positions must avoid all of the above,
across the resolution/aspect-ratio/DPI/UI-scaling variance already
documented in `dota-cv`'s `resolution-and-scaling.md`.** This is the same
underlying geometry problem from the opposite direction — where
`dota-cv` needs to correctly *find* these regions to read them, this
skill needs to correctly *avoid* them when placing overlay content. Reuse
the same geometry/coordinate-space utilities where practical (see
`window-behavior.md`'s DPI-awareness notes) rather than maintaining a
second, independently-tuned notion of where the HUD sits.

**Safe default zones** are typically the areas the base Dota HUD doesn't
use by default — commonly the upper-middle area of the screen (above the
top hero bar, below any killfeed), and the areas flanking the central
play area that aren't claimed by minimap/inventory/ability bar. Validate
this empirically against real captured layouts (the same kind of
known-capture library `dota-cv`/`dota-testing` maintain) rather than
assuming a "probably empty" region is actually safe for every
resolution/aspect-ratio/UI-scale/HUD-skin combination — Dota supports
custom HUD skins that can shift what's actually safe.

## Draggable configuration

**Requirement:** the player can reposition overlay components (or
logical groups of them) to fit their own setup and preferences, since
what counts as a safe zone varies somewhat by player (custom HUD skins,
personal eye-tracking habits, monitor setup).

**How this interacts with the safe-zone rule:** draggable positioning
is a player *convenience/override*, not a rule *exemption*. Concretely:
- Default positions must be chosen to be safe per the guidance above,
  before the player ever touches configuration — a player should never
  need to immediately reposition components just to be able to play.
- When the player drags a component, the configuration UI should warn
  (not silently allow) a position that overlaps a known essential HUD
  region, based on the same geometry data used for default placement —
  don't build draggable positioning as an unconstrained free-for-all that
  bypasses the safe-zone logic entirely just because the player initiated
  the move.
- Configuration/dragging happens in an explicit configuration mode
  (entered deliberately, not during active competitive play — see
  `window-behavior.md`'s click-through notes), never as an always-active
  drag affordance layered on top of the normal live-play click-through
  behavior.

## Compact vs. expanded mode and safe zones

Per `SKILL.md`'s minimal-UI guidance, compact mode's smaller footprint
should make safe-zone compliance easier by construction — a small,
glanceable component naturally has more placement flexibility than a
larger one. Expanded mode (player-initiated, not the default during
active play) has more latitude to use a larger area, but should still
avoid the fixed essential-HUD regions above; expanded mode's larger
footprint is a reason to be *more* careful about safe-zone placement, not
an exemption from the rule, since a larger component has more
opportunity to accidentally overlap something essential.

## Testing implication

Per `dota-testing`, verify default (and any suggested/snapped drag-target)
component positions against the known-HUD-layout capture library across
multiple resolutions/aspect-ratios/UI-scale settings — a safe-zone
regression is the overlay-side equivalent of `dota-cv`'s ROI-geometry
regression, and deserves the same seriousness of testing given how
directly it affects the player's ability to actually play the game.

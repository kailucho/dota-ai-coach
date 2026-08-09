# Window Behavior Requirements

## Transparent

**Requirement:** the overlay window's background is transparent
everywhere no component is actively rendering content — the game must be
fully visible through any non-content area of the overlay.

**Windows implementation notes:** use a layered window
(`WS_EX_LAYERED`) with per-pixel alpha (via `UpdateLayeredWindow` or the
WPF/WinUI equivalent compositing approach) rather than a single window-
level opacity/color-key trick — per-pixel alpha is what allows individual
components to have soft edges/shadows without the whole window taking on
uniform transparency. Verify actual per-pixel transparency behaves
correctly under DPI scaling (see below) — a transparency approach that
looks right at 100% DPI can misbehave at other scale factors if not
implemented carefully.

## Always-on-top

**Requirement:** the overlay stays above the game window without the
player managing window order themselves, including immediately after
game-state changes that might otherwise cause a z-order reshuffle (e.g.
returning from a loading screen).

**Windows implementation notes:** use `WS_EX_TOPMOST` (or the WPF/WinUI
`Topmost` property), and actively re-assert topmost status on relevant
triggers (e.g. periodically, or on detecting the game window regaining
foreground) rather than setting it once at startup and assuming it holds
— some game/fullscreen transitions can disrupt topmost ordering, and the
overlay should recover from that without player intervention.

## Click-through

**Requirement:** mouse input passes through the overlay to the game
everywhere the overlay isn't currently showing an interactive element
(e.g. a drag handle in configuration mode, or an expanded-mode
interaction target). The overlay must never accidentally intercept a
click meant for the game.

**Windows implementation notes:** use `WS_EX_TRANSPARENT` for the
click-through regions, toggled dynamically for any specific sub-region
that needs to accept input (e.g. during layout configuration — see
`hud-safe-zones.md`'s draggable-configuration section). Default to fully
click-through during normal live play; only the explicit configuration
mode (entered deliberately, not during active gameplay) should accept
mouse input across the overlay's components.

## Does not steal focus

**Requirement:** the overlay must never take keyboard focus or bring
itself to foreground activation in a way that pulls input away from the
game — not on startup, not when a new component/alert appears, not on
any content update.

**Windows implementation notes:** combine `WS_EX_NOACTIVATE` with the
topmost/layered/transparent flags above, and audit every code path that
shows or updates a component (especially a newly-appearing P0 alert per
`dota-live-coach`) to confirm it uses a non-activating show/update
mechanism — a naive "show this new alert" implementation using a
default window-show API can activate the window as a side effect even
if the window was originally created with `WS_EX_NOACTIVATE`, depending
on the specific API called. Test this specifically (not just at
overlay-startup, but for every kind of content-triggered visibility
change) since it's an easy rule to violate accidentally partway through
a feature's implementation, well after the initial window creation code
got it right.

## DPI aware

**Requirement:** the overlay renders at the correct size and position
across different Windows DPI scaling settings (100%/125%/150%/etc.), and
stays correctly positioned relative to the game window regardless of
scale factor.

**Windows implementation notes:** declare per-monitor DPI awareness (V2,
where available) in the application manifest, and handle DPI-change
notifications at runtime (a player can change scaling, or move the
overlay to a differently-scaled monitor, without restarting the app) by
re-computing layout rather than assuming DPI is fixed for the process
lifetime. This is the same underlying problem `dota-cv`'s
`resolution-and-scaling.md` addresses for screen capture — reuse
equivalent geometry-handling logic/utilities where practical rather than
maintaining two independent solutions to the same coordinate-space
problem.

## Multi-monitor

**Requirement:** the overlay tracks and renders correctly relative to
whichever monitor the game window is actually on, including setups where
monitors have different resolutions/DPI/refresh rates from each other.

**Windows implementation notes:** determine the overlay's target monitor
from the game window's actual current position (not a fixed/assumed
primary-monitor default), and re-evaluate this if the game window moves
to a different monitor during a session (e.g. player drags the game
window, or a monitor configuration changes). Layout/positioning
calculations (`hud-safe-zones.md`) must be computed relative to the game
window's own bounds and the target monitor's actual DPI, not the primary
monitor's — a common bug class in multi-monitor-aware apps is silently
using primary-monitor DPI/geometry for calculations regardless of which
monitor content is actually rendering on.

## Testing implication

Per `dota-testing`, these behaviors are best verified with both targeted
unit/integration tests where feasible (e.g. DPI-scaling math, monitor-
targeting logic) and manual verification across representative
configurations before shipping a change that touches window behavior —
particularly focus-stealing and click-through, which are easy to
regress silently through a seemingly-unrelated change elsewhere in
component rendering code, and which are high-cost to get wrong (a
focus-stealing overlay actively disrupts competitive play).

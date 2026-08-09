# The Five Primary Live Components

Each component below renders content decided entirely upstream — this
file governs presentation (what it looks like, when it appears, how
compact vs. expanded mode differ for it), not content logic. If a
component seems to need new content logic to do its job well, that
belongs in `dota-item-recommender`/`dota-draft`/`dota-domain`/
`dota-live-coach`, not here.

## NextItemCard

**Shows:** the current top item recommendation, compressed to
`dota-live-coach`'s message style (e.g. `BKB in 400g.`).

**Source:** `dota-item-recommender`'s top-ranked candidate, filtered
through `dota-live-coach`'s priority/cooldown/threshold gating before it
ever reaches this component — the component itself should not be
deciding whether to show a new recommendation, only rendering what's
already been decided to show.

**Compact mode:** the single compressed line, minimal chrome (item
icon + short text). This should be the default, resting presentation
during active play.

**Expanded mode:** player-initiated detail view — may show
`dota-item-recommender`'s `alternatives` and relevant `reasonCodes` in
readable form, since this mode is explicitly the non-live-attention-
constrained surface `dota-live-coach` designates for richer explanation.
Never auto-expands based on recommendation content (e.g. "this one's
important, so show more detail automatically") — expansion is a player
action, not a system decision, per `dota-live-coach`'s explicit
opt-in-only expanded-mode rule.

## PowerSpikeCard

**Shows:** upcoming/active power-spike window information (per
`dota-domain`'s power-spike concept), e.g. `Power spike ready.`

**Source:** `dota-domain`-grounded timing analysis, gated through
`dota-live-coach`'s P2 (informational) tier by default — power-spike
awareness informs the player's own judgment rather than directing a
specific action, matching `dota-live-coach`'s P2 definition.

**Compact mode:** brief state indicator (icon/short text), lowest visual
weight of the five components consistent with its P2 tier.

**Expanded mode:** may show which specific factors (level, item
completion, cooldown availability) are driving the spike assessment.

## BuybackAlert

**Shows:** buyback availability/status, e.g. `No buyback.`

**Source:** directly derivable from `MatchState`'s own-player gold/
buyback-cooldown state (per `dota-gsi`) combined with `dota-domain`'s
buyback strategic guidance for when this matters enough to surface.
Typically **P0** per `dota-live-coach`'s tiers, given how costly a missed
buyback read can be — this is one of `dota-live-coach`'s own worked
examples of P0-tier content.

**Compact mode:** the default and primary presentation — a P0 alert
should be immediately legible without requiring expansion, consistent
with `dota-live-coach`'s "act-now" framing for this tier. Visual
treatment should reflect P0's elevated priority (e.g. distinct visual
weight/positioning) without resorting to anything that risks stealing
focus or violating click-through (`window-behavior.md`).

**Expanded mode:** rarely needed for this component given how simple its
content is — if expansion is offered at all, it should add minimal
additional detail (e.g. buyback cost/cooldown remaining), not turn a
critical, glanceable fact into something requiring more reading time
during an urgent moment.

## PlanCard

**Shows:** the current short-term plan as a sequence, e.g. `Farm → BKB →
fight.`

**Source:** a synthesized short plan spanning `dota-item-recommender`
(the item step) and broader `dota-domain` strategic reasoning (the
farm/fight framing) — this component is the overlay's presentation of
`dota-live-coach`'s arrow-sequence message pattern specifically, so its
content should already arrive pre-compressed in that shape rather than
requiring the component itself to do any summarization.

**Compact mode:** the single arrow-sequence line.

**Expanded mode:** may break down each step with brief supporting
reasoning, but should stay well short of a full strategic essay — even
expanded mode inherits `dota-live-coach`'s general concision preference,
just with slightly more room than the live-message budget allows.

## ThreatIndicator

**Shows:** currently relevant enemy threat information, tied to
`dota-item-recommender`'s enemy-hero/visible-enemy-item context inputs
(e.g. a specific observed threat worth being aware of right now).

**Source:** must be sourced only from `dota-fairplay`-legitimate data —
visible enemy heroes/items (`VISIBLE_SCREEN`/`LIVE_ALLOWED`), never an
inferred/estimated threat. This component is a natural place for a
`dota-fairplay` violation to surface if upstream data sourcing is wrong,
so treat any change touching this component's data source as a
live-data change requiring FairPlay validation per `dota-testing`'s
Definition of Done, even though the component itself is "just" rendering.

**Compact mode:** a compact indicator (icon + minimal label) for the
single most relevant current threat, consistent with
`dota-live-coach`'s 1-3-message live budget — this component competes
for budget slots alongside the others, it doesn't get an independent
allowance.

**Expanded mode:** may list multiple observed threats with brief
per-threat context, useful for a between-fights review of "what am I
actually up against right now."

## Cross-component notes

- All five components share the overlay's window-behavior requirements
  (`window-behavior.md`) and safe-zone placement rules
  (`hud-safe-zones.md`) — there's no per-component exemption from either.
- The 1-3-live-message budget (`dota-live-coach`'s core rule) applies
  across all five components collectively during active play — showing
  all five simultaneously, all the time, would itself violate that
  budget regardless of each individual component's own content being
  correctly gated. Only components with something currently worth
  showing (per their own gating) should be visible at once, and the
  total visible count should respect the shared budget.
- Compact mode is each component's default; expanded mode is always
  player-initiated per component (or per logical group), never a global
  always-expanded setting during active competitive play, consistent
  with `SKILL.md`'s "prefer minimal UI" guidance.

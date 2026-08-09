# Required Context Inputs

Each input below exists because omitting it produces a specific,
predictable class of bad recommendation. When implementing or reviewing
recommendation logic, check that each of these is actually reaching the
scoring stage — a context input that's *available* upstream but not
actually wired into scoring provides none of its intended benefit.

## Hero and role

An item's value is inseparable from the hero using it and the job that
hero is doing (per `dota-domain`'s position/role framework). A str/agi/int
distinction alone is far too coarse — two heroes of the same primary
attribute can want completely different items because their kits use
stats differently (e.g. one scales off attack speed, another off cast
frequency). Role additionally shapes priorities independent of hero kit:
a support's gold constraints and priorities differ from a carry's even
when their kits are superficially similar.

## Facet

Per `dota-domain`'s draft-and-matchups guidance, a facet can meaningfully
change a hero's effective kit — damage type, added mechanics, scaling
stats. Item scoring logic that doesn't account for the hero's active
facet is scoring against a kit that isn't actually in play this game.

## Patch

Item costs, mechanics, and hero balance all move with patches. A scoring
model or rule set trained/tuned against one patch's data will confidently
recommend items whose value has since changed. This is the same rule
`dota-data`/`dota-ml` already enforce for training data (always attach
patch, patch-pin reference-data joins) — item recommendation is exactly
the kind of consumer that rule protects, and scoring must join item/hero
reference data by the *current* patch, not a cached older one.

## Bracket

What's mechanically/strategically correct doesn't always match what's
practically correct at a given skill level — a build requiring precise
execution or reads may be theoretically strong but a poor real-world
recommendation for a bracket where the assumptions behind it (e.g. enemy
coordination, enemy itemization sophistication) don't hold. Bracket
context should inform not just *which* items are recommended but how
confidently (see `confidence-and-bias.md`).

## Minute and level

Timing determines both what's actually reachable (gold/level
requirements) and what's strategically called for right now (per
`dota-domain`'s power-spike and item-timing guidance). A theoretically
ideal end-game item recommended at minute 8 is not a useful next-item
recommendation, even if it would eventually be correct to buy.

## Gold (current and near-future income)

A recommendation the player can't afford soon isn't a *next* item
recommendation — it may be a legitimate long-term build target, but that's
a different kind of output (see `references/output-schema.md`'s
alternatives field for how to represent "good eventually, not next" vs.
"good right now"). Consider near-future income (farm rate, upcoming
lane/jungle opportunity), not just current gold, so recommendations don't
myopically favor only what's affordable this instant.

## Current inventory

Recommending an item already owned, or one that's functionally redundant
with an owned item (e.g. two items granting the same unique-attack
modifier that don't stack), is a straightforward correctness bug.
Candidate generation should exclude these outright (see `pipeline.md`)
rather than relying on scoring to rank them low — an item that's
literally unusable shouldn't reach scoring at all.

## Purchase sequence

The order items were bought is signal, not just history: it reveals what
problem the player already tried to address and whether it worked (e.g.
buying an early defensive item suggests the player identified a specific
threat — did that threat get addressed, or does it persist?). Purchase
sequence also interacts with item-timing reasoning (`dota-domain`) —
recommending an item that "should" have come earlier is a different
signal than recommending the natural next step in an on-track build.

## Allied heroes

Synergy and coverage: recommending redundant utility (e.g. a second
source of the same buff/disable type the team already has) is usually
lower-value than filling an actual gap. Scoring should consider what the
team composition already provides, not just what's generically good for
this hero in isolation.

## Enemy heroes

Counters are matchup-specific, not generic (per `dota-domain`'s
counter/matchup reasoning) — what's good against one enemy draft can be
weak against another. Scoring must condition on the actual observed enemy
heroes, not a generic "average enemy" assumption.

## Visible enemy items

What the enemy has already itemized changes what threats are live right
now (e.g. an enemy who bought a disable-piercing item changes how much a
hard-CC-reliant plan should be trusted). This must be sourced only from
data classified `VISIBLE_SCREEN` or `LIVE_ALLOWED` per `dota-fairplay` —
never from a data source that would expose items the player hasn't
actually seen. See `dota-fairplay`'s worked example on exactly this case.

## Damage composition and disable composition

Of both teams, not just the enemy — a recommendation should account for
what damage types and disable types are already covered by allies (so it
doesn't recommend redundant coverage) and what the enemy is bringing (so
it recommends coverage for real, current threats) — per `dota-domain`'s
damage-type and CC-taxonomy guidance. This is a composition-level
analysis, not a single-hero-vs-single-hero comparison.

## Power spikes

Whose power-spike window is active or imminent (per `dota-domain`)
should shape both *what* item and *how urgently*. An item that shores up
a weakness right before an enemy's spike window is more urgent than the
same item recommended with no such window pressure.

## Player comfort

A player's familiarity with a build affects whether it's the right
real-world recommendation, not just the theoretically optimal one — a
less "optimal" but well-practiced build executed correctly regularly
beats a theoretically stronger build the player is unfamiliar with and
executes poorly. This is genuinely hard to source cleanly; treat it as a
weighting factor (deprioritize confidently recommending something wildly
outside a player's typical build pattern without flagging that it's a
deviation) rather than a strict filter, and be explicit in
`reasonCodes`/`confidence` when comfort data is unavailable or thin.

## Handling missing context

Missing context is common and should be handled explicitly, not silently
defaulted:
- If a required input is unavailable (e.g. enemy items not yet observed
  this game), scoring should reflect reduced confidence
  (`confidence-and-bias.md`), not silently assume a generic/average value
  as if it were real data.
- `reasonCodes` should be able to represent "recommended despite limited
  information on X" so the explanation stage (and the player) can see
  where the recommendation is less grounded, rather than presenting every
  recommendation with uniform apparent certainty.

# Objectives, Scaling, Tempo, and Player-Level Factors

## Roshan

A composition's real capacity to contest and secure Roshan safely (per
`dota-domain`'s Roshan guidance) — this needs enough damage/sustain to
kill it faster than the enemy can arrive to contest, and enough vision/
map control to attempt it without excessive risk. When evaluating a
draft, ask whether the composition can realistically convert a Roshan
kill into a game-winning window (via Aegis-enabled aggression) — a
composition that can kill Roshan but has no plan for what to do with the
Aegis window is getting less value from this dimension than one that
does.

## Tower damage and high ground

Distinct from open-field team-fight strength (per `dota-domain`'s
high-ground guidance): a composition can dominate fights in the open map
and still lack the siege damage, area control, or reset-denial tools to
actually break a defended high ground. Evaluate whether the draft has a
credible plan to close out a lead, not just to accumulate one — this is
a common gap worth flagging explicitly (a composition that's ahead but
structurally unable to finish is a real, common failure mode worth
surfacing to the player, not assuming "ahead" implies "winning").

## Scaling and tempo

**Scaling** — how a composition's power trends over time (early-strong/
late-weak vs. the reverse, or a composition that scales relatively evenly).

**Tempo** — the rate at which the composition wants to convert time into
map advantage (per `dota-domain`'s tempo guidance) — high-tempo drafts
want to fight early and often; low-tempo drafts want to stall toward a
later power spike.

Draft analysis should check that scaling/tempo profile is *internally
consistent* with the composition's other properties: a composition with
strong initiation/control but weak wave clear and low tempo tendencies
may be sending mixed signals about its actual game plan, worth flagging.
It should also check consistency against the emerging **win condition**
(per `dota-domain`) — does this pick support the plan the draft is
already building toward, or does it pull the composition toward a
different, competing plan?

## Farm requirements

How many cores this composition needs meaningfully fed to function (per
`dota-domain`'s farm-priority guidance), and whether the draft can
realistically supply that farm given lane assignments and hero count at
each priority level. A composition with three farm-hungry cores is a
real liability if the supporting cast can't create the space/farm
priority to feed all three — this is a draft-level coherence check, not
just an individual-hero evaluation.

## Execution difficulty

Some heroes/compositions demand precise mechanical execution (tight
skill-shot reliance, complex combo sequencing) or precise decision-making
(a hero whose value depends heavily on correctly reading enemy cooldowns/
positioning). A theoretically strong pick that exceeds what this specific
player can reliably execute is a worse real-world recommendation than a
slightly weaker, more executable one — this is the draft-analysis
counterpart to `dota-item-recommender`'s player-comfort guidance, applied
at pick time instead of purchase time. Feed this into `ExecutionFit` (see
`pick-score-factors.md`).

## Personal hero comfort

Familiarity with the *specific hero* — a player's track record, win
rate, and self-reported comfort with this hero specifically. Distinct
from `RoleExperience` (comfort with the *role/position*, independent of
which specific hero fills it) — see `pick-score-factors.md` for why
keeping these separate matters (a comfortable pos-1 player picking an
unfamiliar pos-1 hero is a different situation from an unfamiliar player
being asked to fill pos 1 at all).

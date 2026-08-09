# Analysis Dimensions

Each dimension below is a lens for post-game review, grounded in
`dota-domain`'s strategic vocabulary. The goal at this stage is
generating candidate observations — what actually happened and how it
compares to a reasonable reference — not yet deciding what's worth
telling the player (that's `insight-selection.md`'s job).

## Laning

Analyze lane outcome (XP/gold differential at lane-phase end, whether the
player was forced off lane, kills/deaths during laning) against
`dota-domain`'s lane guidance for the specific lane assignment played.
Compare against the relevant comparison group (`comparison-groups.md`)
for the same hero/role/lane assignment — a lane outcome is only
meaningfully "good" or "bad" relative to what's typical for that
matchup, not an absolute number.

## Farm

Analyze farm efficiency (GPM, last-hits, farm pattern over time) against
what's expected for the player's role/farm-priority (per `dota-domain`'s
farm-priority guidance) — a support's farm profile should not be judged
against a carry's expectations, and vice versa. Look for farm-priority
*conflicts* within the team (multiple cores competing for the same
resources) as a specific, checkable pattern, not just an individual farm
number in isolation.

## Item timings

Compare actual item completion times against a patch/bracket-appropriate
reference timing (per `dota-domain`'s item-timing guidance and
`comparison-groups.md`'s patch/bracket segmentation) — flag timings that
were meaningfully later than typical for the hero/role/bracket, and
connect a late timing to its likely cause where derivable (lost lane,
farm-priority conflict, defensive item purchase in response to a specific
threat) rather than presenting a bare "you were late" without context.

## Deaths

Analyze each death's context, not just a death count: was it a reasonable
risk given the game state, a positioning error, a forced trade, or a
consequence of a missing buyback/support presence. Per `dota-domain`'s
team-fight guidance, look specifically at whether the death was preceded
by good/bad initiation, target-priority handling, and resource state
(cooldowns, BKB status) — a death count alone conflates very different
underlying causes and isn't itself actionable.

## Teamfights

Analyze fight outcomes against `dota-domain`'s team-fight guidance:
initiation quality (who started the fight and on what terms), target
priority (was damage/CC directed at the highest-value reachable target),
and resource state on both sides. Identify fights that were lost despite
a favorable setup (a specific, correctable execution issue) separately
from fights that were lost due to a structural composition/timing
disadvantage (not really an execution issue at all) — conflating these
produces misleading feedback.

## Objectives

Analyze Roshan/tower/high-ground-related decisions against
`dota-domain`'s guidance — was a Roshan attempt well-timed relative to
vision/contest risk, did the team have the tools to convert a lead into
actual high-ground progress, were objectives taken in a way that served
the draft's win condition. This dimension often explains *why* a team
that was ahead on kills/net worth didn't close out the game, which is a
frequently under-examined and genuinely actionable pattern.

## Buyback

Analyze buyback usage/availability at key moments (per `dota-domain`'s
buyback guidance) — was buyback available and unused during a critical
window, was it used reactively vs. proactively, did a lack of buyback
gold reserve cost the team a fight it could have otherwise contested.
Connect directly to `dota-live-coach`'s `BuybackAlert` component where
relevant — a repeated post-game pattern of buyback mismanagement is a
strong candidate for reinforcing the live coach's messaging, not just a
one-off observation.

## Power spikes

Analyze whether the player's own power-spike windows were used
effectively (did the team fight/push during the window, or let it pass
unused) and whether the enemy's spike windows were defended against
(did the team play safely around a known enemy spike, or get caught by
it). This dimension connects directly to `dota-domain`'s tempo/win-
condition guidance — a missed power-spike window is often a specific,
correctable decision-timing issue.

## Turning points

Identify specific moments where the game's trajectory measurably shifted
(a lost/won fight that changed net worth or map control trajectory, an
objective trade, a key death). A turning-point analysis should connect a
turning point to the dimensions above (was it a teamfight execution
issue, a missed power spike, a buyback failure) rather than presenting
the turning point in isolation — the value of identifying a turning point
is in explaining *why* it happened, which is what makes it actionable.

## Recommendation adherence

See `recommendation-adherence.md` for the specific care needed here —
this dimension compares what the live coach recommended
(`dota-item-recommender`/`dota-live-coach`) against what the player
actually did, and needs to avoid two symmetric traps: assuming
non-adherence was a mistake, and assuming adherence was automatically
correct.

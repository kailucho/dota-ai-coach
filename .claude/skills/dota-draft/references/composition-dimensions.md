# Composition Dimensions

These are properties of a *team composition* (both the drafting team and
the enemy), not properties of a single hero in isolation. A hero's value
along each dimension depends on what the rest of the composition already
provides — the same hero can fill a critical gap on one draft and be
redundant on another.

## Counters

A counter claim should decompose to a specific mechanism, per
`dota-domain`'s counter guidance — spell-immunity denying a
disable-reliant hero, a silence denying a spellcaster, an itemization
answer to a specific damage type. When draft analysis flags "hero A
counters hero B," it should carry the mechanism as structured reasoning
(feeding a `reasonCode`, per `dota-item-recommender`'s output pattern),
not just a statistical label. This is what makes the counter check
resistant to the core rule's failure mode — a mechanism-based counter
claim is checkable against this specific draft; a bare statistic is not.

## Synergies

Same standard as counters: a synergy claim should identify the specific
combo (this disable enables that follow-up, this aura specifically
amplifies that hero's scaling) per `dota-domain`. When evaluating
`DraftFit` (see `pick-score-factors.md`), synergy with heroes *already
picked* on the same team should carry real weight — a hero that's
individually strong but synergizes poorly with the existing four picks is
a worse `DraftFit` than a slightly weaker hero that combos well.

## Lane matchups

The specific lane a pick is likely to occupy (based on role/position and
what's already drafted) determines whether it wins, survives, or loses
its lane — and lane outcome materially changes the rest of the game (per
`dota-domain`'s lane guidance). Draft analysis should reason about the
*implied* lane assignment given the picks so far, not just the hero in
isolation — the same hero can have a favorable or unfavorable lane
matchup entirely depending on what's lined up opposite it.

## Damage distribution

Across the whole team, not hero by hero: is the team's damage
concentrated in one type (all-physical, all-magical) in a way the enemy
can itemize against wholesale, or is it distributed enough to force the
enemy into multiple, more expensive itemization answers? Per
`dota-domain`'s damage-type guidance, this is a composition-level
question — evaluate a new pick partly by whether it improves or worsens
the team's existing distribution, not just by its own damage type in
isolation.

## Control

Coverage and quality of disables across the team (per `dota-domain`'s CC
taxonomy — hard vs. soft, duration, range, spell-immunity piercing).
Composition analysis should check both *quantity* (is there enough
control to lock down a priority target) and *quality* (is the control
reliable — long range and hard disable — or easily avoided/dispelled).

## Initiation and counter-initiation

**Initiation** — does the team have a reliable way to start a fight on
favorable terms (a long-range stun, a jump-in, an area disable)? A
composition with no reliable initiator struggles to force fights on its
own terms even if individually strong.

**Counter-initiation** — a distinct capability: can the team punish or
disrupt an *enemy's* initiation attempt (a hero who can interrupt a
channel, a save that neutralizes the initiator's target selection)? A
composition can have strong initiation and weak counter-initiation, or
vice versa — evaluate both separately, since they answer different
questions ("can we start fights" vs. "can we survive being started on").

## Save

The capacity to protect a specific threatened teammate (a dispel, a
displacement, a shield, a heal timed to prevent a kill) — distinct from
general sustain (below) because save is about a specific, timely
intervention for a specific target, not aggregate healing throughput. A
composition heavy on high-value, easily-focused targets (e.g. a
core-heavy draft with fragile heroes) needs save capacity more than a
composition that's individually tankier.

## Sustain

Aggregate healing/lifesteal/regen capacity across the team, relevant to
winning extended fights and sieges (per `dota-domain`'s team-fight and
high-ground guidance) rather than a single burst moment. Evaluate whether
the composition can out-sustain likely enemy damage output over a
prolonged engagement, not just win a fast trade.

## Wave clear

Capacity to clear creep waves quickly, which shapes multiple things at
once (per `dota-domain`'s farm-priority and tempo guidance): defensive
safety (a composition with poor wave clear struggles to hold lanes/
towers under pressure), split-push viability (strong wave clear enables a
composition to pressure the map without full team presence), and overall
tempo (fast wave clear supports a high-tempo plan; a composition relying
on slow wave clear tends to want a slower game plan).

## How to use these in scoring

Evaluate a candidate pick's *marginal* contribution to each dimension
given the composition already drafted — the question isn't "is this hero
good at control in general," it's "does adding this hero improve this
specific team's control coverage, given what's already there." This
marginal framing is what feeds `DraftFit` (see `pick-score-factors.md`)
and is what keeps composition analysis from degenerating into "pick the
generically best hero" — which is exactly the failure mode the core rule
warns against, just applied to composition dimensions instead of raw
counter stats.

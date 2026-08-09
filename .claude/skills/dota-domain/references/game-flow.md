# Game Flow: Tempo, Timings, Objectives

## Power spikes

A power spike is a point where a hero's relative strength jumps —
hitting a key level (e.g. unlocking ultimate at level 6), finishing a
key item, or a talent choice. Power spikes matter because they define
*windows*: a hero is disproportionately strong for a period until the
opponent catches up or out-scales them.

When reasoning about timing advice ("should I fight now"), identify:
- whose power spike is active or imminent (yours or the enemy's)
- whether the current game minute favors the hero being evaluated

A hero recommendation or item recommendation that ignores power spike
timing can be mechanically correct but strategically wrong (e.g. "correct"
item, wrong minute to be building toward it if a fight window closes first).

## Tempo

Tempo = the rate at which a team can convert time into map advantage
(kills, objectives, space). "Winning tempo" means forcing the opponent to
react to you rather than executing their own plan.

- High-tempo heroes/drafts want to fight early and often, before slower
  scaling heroes come online.
- Low-tempo ("late game") drafts want to stall, avoid unnecessary fights,
  and let farm priority heroes reach their spike.

A draft or in-game decision should be evaluated against which team's tempo
plan is currently working — advice that's correct for a high-tempo draft
(e.g. "fight now") can be wrong for a late-game-oriented one.

## Farm priority

Farm priority determines who gets contested resources (jungle camps,
uncontested lane creeps, ancient stack). It is a team-level allocation
problem, not a per-hero one: giving farm to the wrong hero (relative to the
draft's win condition) can lose the farm-priority hero's item timing
advantage entirely, even if that hero is individually strong.

When reasoning about "who should get this farm," consider:
- who is the team's actual win condition (see below), not just who is
  labeled pos 1
- current item timing needs across the team, not just the single hero

## Win conditions

A win condition is the specific plan by which a team expects to win the
game — not a vague "we have a strong pos 1," but something like "hold
until [hero]'s BKB + core item at minute X, then force a five-man fight
around Roshan." Every draft has one; the coaching-relevant question is
usually whether the *current* game state is still advancing that plan or
has drifted from it.

Recommendations (items, fight decisions, objectives) should be evaluated
against whether they serve the team's win condition, not in isolation.

## Roshan

Roshan is a neutral objective that grants gold/XP to the killing team,
drops an Aegis of the Immortal (one free death with revival) and other
possible items, and re-spawns on a randomized delay after being killed.
Because Aegis is time-limited and Roshan's timer is a resource both teams
track, "should we take/contest Roshan" depends on:
- whether the team has enough damage/sustain to kill it faster than the
  enemy can arrive to contest
- whether holding Aegis serves the current win condition (e.g. enabling a
  risky high-ground push)
- vision/information about the enemy's position, since a contested Roshan
  fight is often more dangerous than the Roshan kill itself

## High ground

Attacking a team's high-ground base is structurally harder than
open-field fighting: defenders get chokepoints, tower/barracks damage, and
better vision. A team can be ahead on kills/net worth and still be unable
to close the game if it lacks the tools (siege damage, area control,
disables to prevent defenders from resetting) to break high ground.
Advice like "push now" should account for whether the team's kit is
actually suited to a high-ground siege, not just whether it is currently
winning fights in the open field.

## Buyback

Buyback lets a dead hero respawn immediately for a gold cost that scales
with game time/level, on a cooldown. It is a resource trade: gold for
immediate map presence. Key implications for advice:
- a team with buyback available is not actually "down a player" even
  though a hero is dead — treat this as a live resource, not a permanent loss
- forcing a fight when the enemy carry has no buyback available (spent
  gold, or on cooldown) is a materially different decision than when they do
- recommending an item purchase that leaves a hero unable to afford buyback
  can be a real mistake worth flagging, depending on game state

## Item timings

An item's *value* is inseparable from *when* it's acquired. The same item
finished 5 minutes earlier than the opponent's counterplay is strong; the
same item finished 5 minutes late can be a wasted purchase. When
reasoning about item recommendations, consider:
- what minute/gold the item is realistically achievable at, given current
  farm rate
- what the enemy's timing looks like for their own relevant item/spike
- whether a cheaper, earlier item that enables the current game plan beats
  a stronger, later item that arrives after the relevant window has passed

## Team fights

A team fight is won or lost largely on: who initiates on favorable terms,
whether disables land on the correct priority targets, and whether damage
output before the enemy peaks/backline defense wins the trade. When
evaluating a "should we fight" or post-fight analysis question, look at:
- who had setup advantage (vision, positioning, cooldowns available)
- target priority — was damage/CC directed at the highest-value enemy
  target reachable, not just the nearest one
- resource state on both sides (buybacks, key spell cooldowns, BKB status)

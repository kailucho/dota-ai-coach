# Recommendation Adherence

## The two symmetric traps

This dimension compares what the live coach recommended
(`dota-item-recommender`/`dota-draft`/`dota-live-coach`) against what the
player actually did. It needs particular care because of two symmetric,
equally-tempting mistakes:

**Trap 1: assuming non-adherence was a mistake.** A player who deviated
from a recommendation might have had legitimate reasons the recommendation
system didn't have full visibility into at the time (a read on the enemy
the system's context inputs didn't capture, a mechanical/comfort reason
per `dota-item-recommender`'s player-comfort input, or simply a
correct real-time judgment call that beat the recommendation). Presenting
every deviation as an error is both often wrong and actively
discouraging — it also implicitly claims the recommendation engine is
infallible, which no responsible analysis of it should claim.

**Trap 2: assuming adherence was automatically correct.** A player who
followed a recommendation and won didn't necessarily win *because* of
following it — this is the exact correlation-vs-causality trap
(`correlation-vs-causality.md`) applied specifically to recommendation
adherence. "You followed the BKB recommendation and won" carries the same
causal-attribution risk as any other single-game correlation.

## How to analyze this dimension correctly

- **Compare outcomes at the comparison-group level, not the single-game
  level.** Per `correlation-vs-causality.md`, an association-tier claim
  ("players in similar situations who followed this kind of
  recommendation won more often") is legitimate to compute and, with
  appropriate hedged phrasing, to surface — a single game's adherence-
  outcome pairing is not.
- **When a player deviated, analyze the deviation's actual outcome
  neutrally** — did the situation the player reacted to (if identifiable
  from match data) suggest their read was reasonable, using the same
  `dota-domain`-grounded mechanism reasoning `dota-item-recommender`
  itself uses for scoring, rather than defaulting to "the system's
  recommendation must have been right."
- **Track adherence patterns over time, not just per-game**, since a
  repeated pattern (e.g. consistently deviating from timing
  recommendations in a specific way) is more informative and more
  actionable than any single instance — but apply
  `sample-size-and-confidence.md`'s discipline before presenting a
  pattern claim as established.
- **Feed genuinely identified recommendation-engine gaps back
  upstream.** If post-game analysis repeatedly finds that a specific kind
  of deviation reliably outperformed the recommendation (at
  association-tier confidence, appropriately sample-sized), that's
  valuable signal for improving `dota-item-recommender`/`dota-draft`
  itself — this dimension isn't just about coaching the player, it's a
  feedback loop for the recommendation systems, and should be treated as
  such rather than only ever pointing blame at the player's deviation.

## What NOT to do

- Don't build a simple "adherence rate" metric and present it as a
  quality signal on its own (e.g. "you followed 80% of recommendations")
  — adherence rate alone says nothing about whether following was correct
  in each case, and presenting it without that context implies a value
  judgment the data doesn't actually support.
- Don't let this dimension's presence create pressure (explicit or
  implied) for the player to blindly follow every live recommendation in
  future games — the live coach's own recommendations already carry
  appropriate confidence/reasoning (per `dota-item-recommender`'s
  `confidence` field); this dimension's job is honest post-hoc analysis,
  not reinforcing unconditional compliance.

## Relationship to insight selection

Recommendation-adherence findings compete for the same scarce 1-3
insight slots as every other dimension (`insight-selection.md`) — they
don't get a reserved slot just because they're about the coaching system
itself. A genuinely well-supported, actionable adherence-related insight
can and should make the cut; a weak or poorly-tiered one shouldn't, by
the same standard applied to every other dimension.

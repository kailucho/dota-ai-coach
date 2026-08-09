# Final Pick Score: Six Factors

## Why six separate factors, not one blended score

Each factor answers a genuinely different question, draws on different
data, and matters differently depending on context — collapsing them
into one opaque number early loses information a consumer might actually
need. Keep them computed and exposed separately (structured output, per
`dota-item-recommender`'s pattern) so:

- a reviewer or the player can see *why* a hero scored well (e.g. high
  MetaStrength but low PersonalComfort, vs. the reverse) instead of just
  a final number;
- different product contexts can weight them differently without
  redesigning the underlying scoring — a coaching tool for a player
  actively trying to expand their hero pool might deliberately downweight
  PersonalComfort/RoleExperience in favor of MetaStrength/DraftFit; a
  tournament-prep tool for an experienced team might do the opposite;
- each factor can be tested, debugged, and improved independently — a
  bug in how LaneFit is computed shouldn't require touching MetaStrength
  logic to fix.

## The six factors

### MetaStrength

How strong the hero is in the current patch and bracket, in general —
independent of this specific draft. This is where global/aggregate
statistics legitimately belong, but bounded: they inform this one factor
among six, never the whole recommendation (this is the structural
enforcement of the core rule — global counter/win-rate stats can't
dominate the final score because they're architecturally confined to one
of six inputs).

**Must be patch-pinned and bracket-segmented** — see
`patch-and-bracket-awareness.md`. A MetaStrength score computed from
all-time, all-bracket data is not meaningfully different from the exact
mistake the core rule warns against, just relabeled.

### DraftFit

How well the hero fits the two compositions already forming — synergy
with allies already picked, counter/anti-synergy against enemies already
picked, and marginal contribution to the composition dimensions in
`composition-dimensions.md` (damage distribution, control, initiation/
counter-initiation, save, sustain, wave clear) and
`objectives-and-scaling.md` (Roshan/tower/high-ground capability,
scaling/tempo/win-condition coherence, farm requirements).

This factor is necessarily draft-state-dependent — it must be
recomputed as the draft progresses, not cached from an earlier point,
since every new pick (ally or enemy) changes what DraftFit means for the
remaining candidates.

### LaneFit

Whether the hero wins or survives its likely lane assignment, given the
implied role/position and what's already drafted on both sides (per
`composition-dimensions.md`'s lane-matchup guidance). This is
specifically about the laning phase outcome, distinct from DraftFit's
broader whole-game composition analysis — a hero can be an excellent
DraftFit for the mid-late game while facing a genuinely difficult lane
matchup, and that tension is worth surfacing rather than averaging away.

### PersonalComfort

This specific player's track record and familiarity with this specific
hero — win rate, games played, self-reported comfort if available. Should
degrade gracefully (lower confidence, not a hard failure) when data is
sparse for a given player/hero pair — a new player or an unfamiliar hero
shouldn't crash the scoring, just produce a properly low-confidence
PersonalComfort contribution (tie into whatever confidence mechanism the
output format uses, consistent with `dota-item-recommender`'s
confidence guidance).

### RoleExperience

This player's track record in the *role/position* being filled,
independent of the specific hero. Keeping this separate from
PersonalComfort matters because the two answer genuinely different
questions:
- comfortable-hero + unfamiliar-role (e.g. a player who knows a hero well
  from one position being asked to play it in a different position) —
  RoleExperience low, PersonalComfort potentially high;
- unfamiliar-hero + comfortable-role (a player picking up a new hero
  within a role/position they play often) — PersonalComfort low,
  RoleExperience potentially high.

A recommendation system that conflates these gives worse advice to
players in either situation than one that reasons about them separately.

### ExecutionFit

Whether the hero's mechanical/decision-making execution demands (per
`objectives-and-scaling.md`'s execution-difficulty guidance) match what
this player can reliably deliver. This is somewhat correlated with
PersonalComfort/RoleExperience but not redundant with them — a player can
be comfortable with a hero's general playstyle while still struggling
with a specific hard mechanical requirement (precise skill-shot
sequencing, tight cancel timing), which ExecutionFit is meant to capture
distinctly.

## Combining the factors

Don't hardcode a single fixed weighting as the only option — expose the
six factors as the structured output (alongside `reasonCodes` explaining
what drove each) and let the combination/weighting be an explicit,
documented, tunable choice appropriate to the product context (coaching
vs. tournament prep, as above). Whatever the default weighting, document
it and its rationale, and make sure the final combined score is never
computed in a way that lets MetaStrength alone dominate — since that
would silently reintroduce the exact failure the core rule exists to
prevent, just laundered through a weighted-average step instead of being
the direct output.

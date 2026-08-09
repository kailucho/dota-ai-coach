# Comparison Groups

## The priority order, and why it's ordered this way

Compare the player primarily against players matching, in this order of
priority:

1. **same hero**
2. **same role**
3. **same bracket**
4. **same patch**

This ordering reflects how much each dimension changes what "normal"
looks like, from most to least specific to the exact situation being
evaluated:

- **Hero** is first because item timings, farm patterns, and much of
  what's being evaluated are hero-specific in ways nothing else fully
  captures (per `dota-domain`'s hero/kit guidance) — comparing a player's
  item timing on a specific hero against a generic same-role baseline
  loses the most important context.
- **Role** is second because even within a hero, if role assignment
  varies (e.g. a flex hero played at different positions), the farm-
  priority/timing expectations shift meaningfully (per `dota-domain`'s
  position guidance) — role is the next most specific narrowing after
  hero.
- **Bracket** is third because execution norms and practical outcomes
  differ meaningfully by skill level (per `dota-draft`'s
  patch-and-bracket-awareness guidance, which applies the same principle
  to draft analysis) — but bracket matters somewhat less than
  hero/role for most of `analysis-dimensions.md`'s categories, since the
  underlying strategic logic (what a good item timing looks like) is
  less bracket-sensitive than it is hero/role-sensitive.
- **Patch** is fourth because balance changes shift specifics (item
  costs, hero power) but the broad strategic pattern being evaluated
  (e.g. "did you use your power spike window") is often patch-stable
  even when the exact numbers aren't — patch still matters and must be
  respected (see below), just typically as the least distinguishing of
  the four dimensions for most insight types.

This ordering is a reasonable default, not an absolute rule for every
insight type — an insight specifically about bracket-dependent execution
norms might reasonably weight bracket more heavily than the default
order suggests. Document any deliberate deviation from the default
ordering for a specific insight type, rather than silently reordering.

## Requiring patch-pinning regardless of priority order

Even though patch is last in priority order, **it must never be
dropped entirely** — comparing against stale-patch data for convenience
(e.g. because current-patch sample size is thin) silently reproduces the
exact problem `dota-data`'s patch-tagging rule and `dota-draft`'s
patch-awareness guidance both exist to prevent. If current-patch data is
too sparse for a clean same-hero/same-role/same-bracket/same-patch
comparison, that's a sparse-comparison-group problem to handle explicitly
(see below), not a reason to silently substitute an older patch's data
as if it were equivalent.

## Handling a comparison group that's too sparse

When the narrowest available comparison group (same hero + role +
bracket + patch) doesn't have enough games to be statistically
meaningful (per `sample-size-and-confidence.md`'s thresholds):

1. **Widen one dimension at a time, in reverse priority order** — drop
   patch-pinning last-resort only if truly necessary (and even then,
   prefer widening to "recent patches" rather than all-time), widen
   bracket next (adjacent brackets), then role, then hero, only as a last
   resort, since hero is the most important dimension to preserve.
2. **Communicate the widened comparison group explicitly** to the
   player/report — "compared to players on similar heroes in your
   bracket" (if hero had to widen) is meaningfully different information
   than "compared to your exact hero," and presenting a widened
   comparison as if it were the ideal one misrepresents the insight's
   basis. This is the same principle as `sample-size-and-confidence.md`'s
   uncertainty-communication guidance, applied specifically to comparison-
   group composition.
3. **Never silently fall back to "all players, all brackets, all
   patches"** as an unstated default just because it has the most data —
   an insight built on a poorly-matched comparison group is often worse
   than no insight at all, since it can actively mislead rather than
   simply being unhelpful.

## Where this data comes from

Comparison-group data must be sourced from `dota-data`'s correctly
patch-tagged, bracket-segmented FEATURES/DATASET pipeline — this skill
consumes that data, it doesn't re-derive patch/bracket segmentation
independently. If a needed comparison-group granularity isn't supported
by the current data pipeline, that's a `dota-data` gap to raise, not
something to approximate locally in a way that risks silently
reintroducing the exact aggregation problems `dota-data`'s and
`dota-draft`'s patch/bracket-awareness rules already guard against.

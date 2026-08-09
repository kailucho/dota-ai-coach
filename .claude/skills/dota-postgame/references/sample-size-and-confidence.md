# Sample Size and Confidence

## Why this matters more in post-game analytics than it might seem

Post-game reports are naturally built around *this player's* history —
which for many players, especially on a specific hero or in a specific
role, means a small underlying sample. A pattern computed from 4 games
looks exactly as clean and presentable as one computed from 400, unless
the system deliberately tracks and communicates sample size — and a
confidently-presented pattern from 4 games is likely to be mostly noise.

## Minimum sample thresholds by claim type

Different claim types need different minimum sample sizes before
they're trustworthy enough to surface at all:

- **"You tend to..." claims about this player specifically** (e.g. "you
  tend to die early in fights on this hero") — need a meaningfully larger
  sample than a single game or two, since individual-player behavioral
  patterns are noisy game-to-game. Don't surface a personal-tendency claim
  from a handful of games; if the sample is thin, either don't surface
  the claim or surface it with explicit, visible uncertainty (see below)
  rather than confident phrasing.
- **Comparison-group association claims** (per `correlation-vs-causality.md`)
  — need enough games in the *reference group* (not just the player's own
  games) to be statistically meaningful; a comparison group with very few
  matching games is itself a sample-size problem, independent of how many
  games the player has personally played. See `comparison-groups.md` for
  what to do when the ideal comparison group is too sparse.
- **This-game-only observations** (e.g. "you died 3 times to gank in the
  first 10 minutes this game") — don't need a sample-size justification
  in the same sense, since they're describing what happened, not making a
  generalizable claim. Keep these clearly separated from claims that *are*
  making a generalization, since conflating "this happened" with "this is
  a pattern for you" is itself a sample-size-blindness mistake even when
  each individual game-level fact is true.

## Communicating uncertainty to the player

When a claim is based on a thin sample but still worth surfacing (e.g.
because it's the best available insight and outright suppressing it
would leave nothing useful to say), communicate the uncertainty
explicitly rather than presenting it with the same confident tone as a
well-supported claim — e.g. "based on a small number of games" or similar
plain-language hedging, not just a hidden confidence number the player
never sees. This mirrors `dota-item-recommender`'s `confidence` field
and `dota-draft`'s bracket-sparse-data guidance — surfacing honest
uncertainty is more useful long-term than a confident-sounding claim that
doesn't hold up.

## Shrinkage for small-sample player-specific estimates

Reuse the Bayesian-shrinkage discipline already established in
`dota-item-recommender`/`dota-ml`: rather than trusting a small-sample
player-specific raw estimate outright, shrink it toward a broader prior
(e.g. the player's own broader history across all heroes/roles, or the
relevant comparison group's typical value) and reflect the resulting
uncertainty in how confidently the insight is presented. This is
particularly relevant for stats that vary a lot game-to-game by nature
(e.g. a single fight-outcome rate) versus stats that are more stable
(e.g. overall farm pattern), so calibrate how much shrinkage/hedging a
given metric needs based on its own natural game-to-game variance, not a
one-size-fits-all threshold.

## Interaction with the 1-3 insight rule

Sample-size discipline and `insight-selection.md`'s 1-3-insight rule
work together: a thin-sample claim that would need heavy hedging to
present honestly is often a weaker candidate for one of the report's
scarce 1-3 slots than a well-supported claim, even if the thin-sample
claim sounds more interesting. When selecting which insights make the
final cut, treat confidence/sample-size as a real input to the
selection, not just a caveat appended after the "interesting" insights
are already chosen.

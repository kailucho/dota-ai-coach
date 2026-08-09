# Confidence, Survivorship Bias, and Selection Bias

## Why historical item data is biased by construction

Players don't buy items at random — they buy items they (or common
guides/trends) already believe are good, often *because* of the exact
situation they're in. This creates systematic bias in any dataset of
"item X was bought, game outcome was Y":

- **Selection bias:** players who bought item X in a given situation
  already had information (comfort, guides, meta knowledge) that led them
  to that choice — the population that bought X isn't a random sample of
  "all situations," it's a sample skewed toward situations where X looked
  appealing for reasons that may correlate with winning independent of
  the item itself (e.g. players ahead in gold buy different items than
  players behind, and being ahead already predicts winning).
- **Survivorship bias:** outcome data disproportionately reflects
  builds/situations that played out to a measurable result; a build that
  reliably leads to very early, decisive losses may be underrepresented
  relative to its true frequency of being attempted, if incomplete/
  abandoned games are excluded (see `dota-data`'s abandoned-game handling)
  without accounting for what that exclusion does to this specific
  question.
- **Confounding with game state:** an item's raw win-rate correlation is
  entangled with *why* it was bought — an item commonly bought when
  already winning will show an inflated win rate that has little to do
  with the item's actual causal contribution.

This is why the core rule (`NextItem != ItemWithHighestWinRate`) isn't
just a strategic simplification to avoid — it's specifically avoiding a
statistically confounded signal that looks authoritative but isn't.

## What to do about it in scoring

- **Condition aggressively, don't just aggregate.** Rather than "item
  X's win rate," use context-conditioned signals — win rate (or a
  better-designed target) for item X *given* hero, role, patch, bracket,
  timing, and ideally *given* the game-state trajectory up to the
  purchase point, not the outcome after. The narrower and more relevant
  the conditioning, the less the selection-bias confound dominates — but
  see the next point, since narrow conditioning creates its own problem.
- **Use Bayesian shrinkage for narrow slices** (per `dota-ml`'s preferred
  approaches) — a narrowly-conditioned estimate (this hero, this facet,
  this patch, this exact matchup) will often have very few observations,
  making the raw estimate noisy and easy to overfit to. Shrink toward a
  broader prior (role-level, hero-level) rather than trusting a small
  sample's raw number, and reflect the resulting uncertainty in
  `confidence` (see below).
- **Prefer features that describe *why* an item might help over raw
  outcome correlation where possible.** A rule like "this item's active
  ability directly counters the enemy's most threatening spell" (encoded
  via `dota-domain` mechanics — damage type, CC piercing, etc.) is not
  subject to selection bias the way "historically, players who bought
  this item against similar heroes won more" is. Favor mechanism-based
  reasoning as a first-class scoring input, not just a tiebreaker on top
  of a purely outcome-driven model.
- **Apply `dota-ml`'s full discipline to any model-based scoring
  component.** Baseline-first, temporal validation, leakage tests — this
  isn't exempt from that just because it's "item scoring" rather than a
  more obviously ML-flavored task. In particular, watch for the specific
  leakage risk of a feature that's only available *because* the item was
  already purchased (e.g. a feature summarizing "the rest of this game's
  build," which wouldn't exist yet at the actual recommendation moment) —
  this is `dota-ml`'s feature-availability rule applied to this domain.

## Computing confidence

`confidence` should reflect the system's trust in a given score, distinct
from the score's magnitude. Concrete inputs to confidence:

- **Sample size** behind the conditioned estimate this score relies on
  (post-shrinkage, still track and expose how much this was shrunk — a
  heavily-shrunk estimate deserves lower confidence than a well-supported
  one, even at the same final score).
- **Context completeness** — how much of the required context
  (`context-inputs.md`) was actually available for this recommendation.
  Missing enemy-item visibility, unclear player-comfort data, etc. should
  reduce confidence, and ideally surface via a `reasonCode` like
  `LIMITED_ENEMY_VISIBILITY` or `LOW_SAMPLE_CONTEXT` so the reduction is
  traceable, not just a number with no explanation.
- **Model agreement**, if scoring combines multiple signals (rule-based
  and model-based) — strong agreement between independent signals
  increasing confidence, disagreement decreasing it, is a reasonable and
  interpretable approach.

Don't present every recommendation with uniform apparent certainty —
a low-confidence recommendation surfaced honestly is more useful (and
more trustworthy over time) than a confident-looking recommendation built
on thin data.

# Structured Output Schema

## Why structure matters here specifically

An item recommendation that's just "buy Black King Bar" with no
structure is unauditable — nobody (not a reviewer, not the explanation
stage, not a future version of this same system) can tell *why* without
re-deriving it from scratch. The fields below exist so every
recommendation carries its own justification and alternatives, per
`dota-domain`'s general preference for structured concepts over free-text
assumptions.

## Field definitions

```
itemId:        Canonical item identifier (per dota-data's item reference
                data), not a display name — display formatting is a UI
                concern, not a recommendation-output concern.

score:         The scoring stage's output for this item, given the full
                context. Not required to be a calibrated probability
                unless the scoring approach specifically produces one
                (per dota-ml's calibration guidance if a model is
                involved) — but its meaning (what higher/lower means,
                its range) must be documented and consistent across all
                items compared in one recommendation call, since ranking
                depends on scores being comparable to each other.

confidence:     How much the system trusts this score, given available
                context. Distinct from score itself: a candidate can
                score very high with low confidence (e.g. strong signal
                but built on thin/missing context, per
                context-inputs.md's "handling missing context") or score
                moderately with high confidence. See
                confidence-and-bias.md for how this should be computed.

reasonCodes:    A list of structured, enumerable reason codes explaining
                why this item scored as it did. Not free text — see the
                taxonomy below. This is what the explanation stage
                consumes, and what makes a recommendation testable
                (a unit test can assert "given this context, item X
                should include reasonCode COUNTERS_ENEMY_BURST" without
                needing to parse natural language).

alternatives:   Other candidates considered, each with their own
                itemId/score/confidence/reasonCodes, ordered by rank.
                This is what lets a player (or a reviewer) see what else
                was weighed and why it ranked lower — critical for
                auditability and for avoiding the appearance that only
                one item was ever considered.
```

## reasonCode taxonomy (starting set — extend as needed, keep enumerable)

Group by what kind of context input drove the reason, so the taxonomy
stays organized as it grows:

**Matchup/threat-driven:**
- `COUNTERS_ENEMY_BURST` — mitigates observed high-burst damage threat.
- `COUNTERS_ENEMY_DISABLE` — addresses a specific observed disable
  threat (e.g. spell immunity vs. a hard-CC-heavy enemy lineup).
- `RESPONDS_TO_VISIBLE_ENEMY_ITEM` — reacts to a specific item observed
  on an enemy (must be sourced from `VISIBLE_SCREEN`/`LIVE_ALLOWED` data
  per `dota-fairplay` — the reasonCode itself should make this
  traceable, not just the underlying data pipeline).

**Team-composition-driven:**
- `FILLS_DAMAGE_TYPE_GAP` — team lacks coverage of a damage type this
  item provides/exploits.
- `FILLS_DISABLE_GAP` — team lacks a disable type this item provides.
- `AVOIDS_ALLY_REDUNDANCY` — deliberately not recommending an item
  redundant with an ally's kit/items (useful on demoted/excluded
  candidates, or as a negative reasonCode explaining a lower rank).

**Timing-driven:**
- `MATCHES_POWER_SPIKE_WINDOW` — supports the hero's own imminent power
  spike, or addresses the enemy's.
- `AFFORDABLE_NOW` vs. `AFFORDABLE_SOON` — distinguishes a genuine
  next-item recommendation from a longer-term build target (see
  `pipeline.md`'s candidate-generation notes and `context-inputs.md`'s
  gold section).
- `ON_TRACK_WITH_PURCHASE_SEQUENCE` — a natural continuation of the
  player's current build path, vs. `DEVIATES_FROM_PURCHASE_SEQUENCE` when
  recommending something that breaks the established pattern (not
  necessarily wrong, but worth flagging explicitly).

**Hero/kit-driven:**
- `SYNERGIZES_WITH_HERO_KIT` — the item's mechanics specifically amplify
  this hero's kit (beyond generic stat value).
- `SYNERGIZES_WITH_FACET` — specific to the hero's currently active
  facet (per `dota-domain`).

**Confidence/data-quality-related:**
- `LIMITED_ENEMY_VISIBILITY` — recommendation made with limited
  observed enemy information (should correlate with reduced
  `confidence`).
- `LOW_SAMPLE_CONTEXT` — scoring for this hero/item/context combination
  is based on a small amount of underlying data (ties into
  `confidence-and-bias.md`'s shrinkage guidance).
- `DEVIATES_FROM_PLAYER_COMFORT` — recommending outside the player's
  typical build pattern for this hero (see `context-inputs.md`'s player
  comfort section).

Keep this taxonomy centrally defined (one canonical list, not
re-invented per call site) so `reasonCodes` stay comparable and testable
across the whole recommendation system, and so the explanation stage can
maintain a stable mapping from code to natural-language phrasing.

## Worked example

```
{
  "itemId": "black_king_bar",
  "score": 0.82,
  "confidence": 0.71,
  "reasonCodes": [
    "COUNTERS_ENEMY_DISABLE",
    "MATCHES_POWER_SPIKE_WINDOW",
    "AFFORDABLE_NOW"
  ],
  "alternatives": [
    {
      "itemId": "aeon_disk",
      "score": 0.74,
      "confidence": 0.68,
      "reasonCodes": ["COUNTERS_ENEMY_BURST", "AFFORDABLE_SOON"]
    },
    {
      "itemId": "hood_of_defiance",
      "score": 0.61,
      "confidence": 0.55,
      "reasonCodes": ["FILLS_DAMAGE_TYPE_GAP", "LOW_SAMPLE_CONTEXT"]
    }
  ]
}
```

This shape lets the explanation stage produce something like "Black King
Bar addresses their hard-CC-heavy lineup and lines up with your power
spike — you can afford it now. Aeon Disk was close behind as a safer pick
against their burst, and Hood of Defiance would help against their
magical damage but that read is based on limited data" — grounded
entirely in the structured fields, not invented.

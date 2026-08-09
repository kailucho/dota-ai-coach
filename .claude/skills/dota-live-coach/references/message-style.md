# Message Style

## Why brevity is a hard requirement, not a preference

During active gameplay, a player's attention is almost entirely consumed
by mechanical execution, positioning, and reading the fight/map. A
message that takes more than an instant to parse either gets ignored or
actively costs the player something (a missed reaction, a mistimed
action) while they read it. This is not the same attention environment
as reading a post-game report or a details panel pulled up between
fights — live messages must be readable in a fraction of a second,
essentially at a glance.

## The reference examples, annotated

```
BKB in 400g.
Farm → BKB → fight.
No buyback.
Power spike ready.
Three dead → objective.
```

What makes these work:
- **No subject pronoun.** Not "You need BKB in 400g" — just `BKB in
  400g.` The player already knows who the message is about.
- **No justification clause.** Not "BKB in 400g because their Bane can
  lock you down" — just the fact/action. The *why* is available on
  request (see "Where explanation goes" below), not pushed into the live
  stream.
- **Arrows for sequence, not sentences.** `Farm → BKB → fight.` conveys a
  three-step plan in four words. This is a deliberate compression
  pattern worth reusing for any message that needs to convey an ordered
  plan rather than a single fact.
- **State, not narrative.** `No buyback.` and `Power spike ready.` state
  a current fact the player can act on/around, without narrating how the
  system determined it.
- **Consequence framing for urgent windows.** `Three dead → objective.`
  connects an observed event to its implication in the same compressed
  arrow pattern, which is often the clearest way to convey "here's what
  just became true and why it matters" in minimal words.

## Writing a new live message

When compressing a recommendation into a live message:

1. **Identify the single most important fact or action.** If a
   recommendation has multiple `reasonCodes` (per
   `dota-item-recommender`), the live message is not a list of them — pick
   the one thing the player needs to know or do right now, and let the
   rest live in the non-live detail view.
2. **Prefer the shortest phrasing that's still unambiguous.** Cut
   articles, pronouns, and hedging words first. "BKB in 400g" beats "You
   might want to consider a BKB once you have around 400 more gold."
3. **Use the arrow pattern for sequences or cause→implication**, per the
   examples above, rather than a full sentence connecting two ideas.
4. **If it can't be compressed below roughly one short line without
   losing the actionable core, it doesn't belong in the live stream at
   all** — that's a sign the content is better suited to P2/on-demand
   detail than to a pushed live message. Don't compress by simply
   truncating a longer message; compress by choosing to say less.

## Where explanation goes

The `reasonCodes`, full scoring breakdown, and any narrative explanation
(per `dota-item-recommender`'s and `dota-draft`'s explanation stages) are
real, valuable output — they just don't belong in the live message
stream. Route them to:
- a details panel the player can pull up on their own initiative
  (between fights, during downtime), never auto-opened or pushed;
- post-game review surfaces (per `dota-postgame`, if/when that skill
  covers this), where the attention constraint that governs live
  messages doesn't apply.

A live message and its full explanation should be linkable (the same
underlying recommendation, two different presentations) rather than the
live message being a lossy, ad hoc summary disconnected from the
richer output — see `dota-item-recommender`'s output schema for the
structured data a live message should be compressing from.

## Tone

Neutral, factual, imperative where it's a recommended action ("Farm →
BKB → fight.") and declarative where it's a fact ("No buyback."). Avoid
encouragement/commentary language ("Nice, you're doing great!") in live
messages — it adds words without adding decision-relevant information,
which directly works against the core rule's attention-budget goal. Save
any encouraging/reflective tone for non-live surfaces where brevity isn't
the binding constraint.

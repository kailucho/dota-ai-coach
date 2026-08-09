# Priority Tiers: P0, P1, P2

## P0 — Critical

**Definition:** time-sensitive, high-stakes, act-now information where
missing the window has an immediate, significant cost. Reserve this tier
deliberately — if everything is P0, nothing is, and the player learns to
ignore the interrupt.

**Examples:** `No buyback.` (before a risky engagement), `Three dead →
objective.` (a rare, high-value window), an imminent, lethal, currently-
unaddressed threat.

**Presentation:** highest visual/audio precedence. May interrupt or
override whatever P1/P2 content is currently shown — a P0 message
displaces lower-priority content rather than stacking alongside it (see
"Combining tiers under the budget" below). Shortest allowed cooldown of
the three tiers (see `anti-spam-mechanisms.md`), because a genuinely
critical, recurring situation (e.g. repeatedly lacking buyback across
multiple risky moments) needs to be able to re-fire when it matters, not
be suppressed by an overly long cooldown tuned for lower-stakes content.

**What does NOT belong at P0:** anything the player has time to notice
and act on without an interrupt, anything speculative/probabilistic
rather than a clear current fact, or anything that isn't genuinely
costly to miss. A recommendation engine's "high score" is not the same
thing as P0-worthy — priority is about presentation urgency, not
recommendation confidence (`dota-item-recommender`'s `confidence` field
is a separate concept and should not be conflated with priority tier).

## P1 — Actionable

**Definition:** a concrete recommended action, worth surfacing promptly
but not urgent enough to interrupt. This is the tier most live coaching
output should land in — a clear "do this" that fits the player's current
decision-making, without demanding they stop what they're doing.

**Examples:** `BKB in 400g.`, `Farm → BKB → fight.`, a clear itemization
or positioning recommendation tied to the current situation.

**Presentation:** normal visual priority, no interrupt. Governed by the
standard cooldown/threshold rules — a P1 message shouldn't restate itself
every time the underlying recommendation is recomputed, only when the
recommended action itself meaningfully changes (see
`anti-spam-mechanisms.md`'s change-threshold guidance).

## P2 — Informational

**Definition:** useful context that doesn't call for immediate action —
situational awareness rather than a directive. This is the first tier
suppressed under attention-budget pressure, and the tier that should be
held to the strictest bar for "is this actually worth a player's limited
attention right now."

**Examples:** `Power spike ready.` — informs the player's own judgment
about upcoming decisions without telling them what to do about it right
this moment.

**Presentation:** lowest visual priority; longest allowed cooldown of the
three tiers. If the 1-3-message budget is under pressure (P0/P1 content
already filling it), P2 messages are the first to be dropped or delayed
— see "Combining tiers under the budget" below.

## Combining tiers under the budget

The core rule's 1-3-message live budget applies across all tiers
together, not per-tier. When multiple messages are candidates for
display simultaneously:

1. **P0 takes precedence and typically displaces other content** — if a
   P0 condition fires, it should generally claim a slot even if that
   means dropping a currently-displayed P1/P2 message, since by
   definition a P0 situation is more urgent than what it displaces.
2. **P1 fills remaining slots** based on relevance/recency — don't just
   show the first three P1 candidates that happened to fire; if the
   recommendation engine surfaces several P1-worthy items, prefer the
   ones most directly tied to the player's next decision (per
   `dota-item-recommender`/`dota-draft`'s scoring — a higher-scored,
   higher-confidence recommendation should generally win a contested
   slot over a lower one).
3. **P2 only fills a slot if P0/P1 aren't already using the full
   budget** — informational content should never crowd out actionable or
   critical content.

## Assigning priority: a practical test

When deciding a new message type's tier, ask: "if the player misses this
message entirely, what happens?"
- **Something costly and immediate** (a death, a lost objective window,
  a wasted buyback) → P0.
- **A missed opportunity to act better**, recoverable, not immediately
  costly → P1.
- **Nothing changes about what the player should do**, just their
  situational picture → P2.

If a message type doesn't clearly fit one answer, it's usually a sign
the message itself is trying to do too much — split it, or push the
excess detail to a non-live surface (per `SKILL.md`'s message-style
guidance) rather than picking an arbitrary tier for a message that's
really doing two jobs at once.

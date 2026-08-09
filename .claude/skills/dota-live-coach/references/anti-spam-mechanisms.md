# Anti-Spam Mechanisms: Hysteresis, Cooldowns, Change Thresholds

## Why all three are needed together

Each mechanism closes a different gap the others don't cover — using
only one leaves a real spam pathway open:

- **Hysteresis alone** stops flicker at a single boundary but doesn't
  stop a message from re-firing frequently if the underlying value
  genuinely oscillates across a wider range over time.
- **Cooldowns alone** stop rapid-fire repetition but don't stop a
  message from re-firing the instant the cooldown expires if nothing
  meaningful actually changed, and don't stop a *different* message from
  firing immediately after (if cooldowns are message-type-scoped rather
  than global).
- **Change thresholds alone** stop trivial restatements but don't stop
  two genuinely-crossing-the-threshold updates from firing in quick
  succession if there's no minimum spacing enforced.

Use all three, layered, for any live message type driven by a
continuously-updating value (most GSI/CV-derived state, per `dota-gsi`).

## Hysteresis

**What it does:** requires a condition to clear a meaningfully different
threshold to *stop* firing than the one that made it *start* firing, so
state doesn't flicker on/off around one boundary value.

**Example:** if "Power spike ready." fires when a countdown reaches ≤10
seconds, don't clear/re-arm the message the instant the countdown ticks
back to 11 seconds (which can happen from minor state jitter or a
recomputation quirk) — use separate enter/exit thresholds (e.g. fires at
≤10s, only re-arms for a future fire once the state has clearly moved on,
such as the spike condition resolving or a new countdown starting), so a
single noisy value near the boundary doesn't produce a rapid on/off/on
message sequence.

**How to implement:** for any boolean-ish live condition (a message
either should or shouldn't currently be showing), track the condition as
a small state machine with distinct "armed," "firing," and "resolved"
states rather than recomputing a fresh boolean from the raw value on
every update — the state machine is what naturally supports asymmetric
enter/exit thresholds.

## Cooldowns

**What it does:** enforces a minimum time between messages, preventing
rapid restatement even for a genuinely fluctuating situation.

**Cooldown scope:** apply per message *type* (not one global cooldown for
the entire live stream), so an unrelated P0 event isn't blocked by a
recent, unrelated P2 message's cooldown — but also consider a shorter
global minimum spacing between *any* two messages, so the player never
gets two separate message types firing at literally the same instant,
which would itself violate the "1-3 messages, easily parsed" spirit of
the core rule even if each individual message respects its own type-
specific cooldown.

**Starting points by tier** (tune against real playtesting, not treated
as fixed forever):
- **P0** — shortest cooldown of the three tiers; needs to remain
  responsive since a recurring critical situation (repeated missed
  buyback windows, repeated three-dead windows) genuinely needs to
  re-fire each time it matters. Still non-zero — even P0 shouldn't be
  literally unthrottled, since a jittery underlying signal could
  otherwise still spam even at this tier.
- **P1** — a moderate cooldown; actionable recommendations don't need to
  restate on every tick, but shouldn't go stale either — reasonable
  starting point is long enough to cover a typical decision window (e.g.
  tens of seconds) rather than a fixed universal number, since "typical
  decision window" varies by message type.
- **P2** — the longest cooldown of the three; informational content is
  the least time-sensitive and should be shown sparingly.

## Change thresholds

**What it does:** a new message only fires if the underlying value
changed by enough to matter for the player's next decision, not on every
minor fluctuation.

**Example:** `BKB in 400g.` shouldn't re-fire every time gold changes by
10 from incremental farm — pick a threshold meaningful to the
recommendation itself (e.g. re-announce only when the estimated
time-to-affordable crosses a round number worth re-stating, or when the
*recommended item itself changes* due to a shifted situation, which is a
much stronger and rarer trigger than "the gold number moved"). Prefer
thresholds tied to what the player would actually do differently
(a materially different ETA, a different recommended action) over
thresholds tied to the raw underlying number's precision.

**How to pick a threshold for a new message type:** ask "how much does
the underlying value need to move before the player's optimal next
action actually changes?" — that's the threshold, not an arbitrary
round number chosen for convenience. If the answer is "it doesn't really
change until a much bigger shift than any reasonable threshold would
prevent," that's a sign this message might be better suited to firing
once (on the initial trigger) with no re-fire logic at all, rather than
needing a threshold-tuning exercise.

## Testing this

Per `dota-testing`, anti-spam logic is exactly the kind of behavior that
needs sequence-based tests (analogous to `dota-gsi`'s replay-simulator
tests) rather than single-snapshot tests — feed a realistic sequence of
fluctuating underlying values through the live-message pipeline and
assert on the *count and spacing* of resulting messages, not just that
individual messages are correctly formed. A test that only checks message
content, never message frequency, will miss the exact class of bug this
skill exists to prevent.

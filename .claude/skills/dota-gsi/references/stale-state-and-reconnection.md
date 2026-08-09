# Stale State and Reconnection

## Why this needs explicit handling

GSI's documented reliability issues (see `payload-structure.md`) mean a
previously-working connection can silently stop sending updates with no
explicit disconnect signal. Without active staleness detection, downstream
consumers (recommendation logic, UI) will keep acting on an increasingly
outdated `MatchState` while believing it's current — which is worse than
an obvious failure, because nothing looks broken.

## Detecting staleness via heartbeat

The `heartbeat` config value (`configuration.md`) guarantees a POST at
least that often even with no state changes. That gives a concrete
staleness signal: if more than roughly `heartbeat` seconds (plus some
tolerance for `timeout`/network jitter) have passed since the last
received payload, the feed should be considered stale, not just quiet.

Track a `LastPayloadReceivedAt` timestamp on ingestion, and expose a
derived staleness/connection-health signal (e.g. `IsStale`,
`SecondsSinceLastUpdate`) alongside `MatchState`, rather than leaving
consumers to infer it themselves from `MatchState`'s contents.

## What MatchState should do while stale

Do not silently keep serving the last-known-good `MatchState` as if it
were current — that's the exact failure mode this detection exists to
prevent. Instead:

- Surface staleness as an explicit, first-class signal alongside
  `MatchState` (not buried in a log line), so any consumer — UI, live
  recommendation logic — can decide how to degrade (e.g. show "reconnecting"
  rather than a live recommendation that's actually based on stale data).
- Don't clear/reset `MatchState` just because it's stale — the last-known
  state is still useful context once the feed resumes, and blanking it
  loses information without a compensating benefit.
- Live recommendation logic (outside this skill's scope, but a downstream
  consumer of it) should treat "stale" as a reason to suppress or caveat a
  recommendation, not proceed as if nothing changed — flag this in review
  if you see recommendation code that doesn't check the staleness signal.

## Reconnection behavior

When payloads resume after a stale period:

- Don't assume the resumed feed picks up exactly where it left off.
  Payloads may have been missed entirely during the gap (see
  `state-diffing.md` — partial-payload merging means missed updates are
  simply never applied, not queued and replayed).
- Because of this, `MatchState` after a reconnection gap may be behind
  reality in ways smaller than a full resync but larger than normal
  update latency. If a section changed multiple times during the gap,
  only the final state (as reflected in the next payload) will ever
  arrive — intermediate transitions are lost, which matters for anything
  deriving `DomainEvents` from transitions (e.g. "hero died" events could
  be missed if the payload covering that moment was dropped).
- Treat a reconnection as an appropriate point to log/flag a
  "state discontinuity" marker, since downstream consumers deriving
  transition-based events should know a gap occurred rather than silently
  assuming continuous coverage.

## Testing implication

Fixture-based tests (`fixtures-and-testing.md`) should include at least
one scenario simulating a gap — a sequence of payloads with a deliberate
time jump or missing intermediate state — and assert that:
1. staleness is correctly detected during the gap;
2. `MatchState` isn't corrupted or reset by the gap;
3. normalization after the gap doesn't crash or produce incoherent state
   just because some intermediate transition was never observed.

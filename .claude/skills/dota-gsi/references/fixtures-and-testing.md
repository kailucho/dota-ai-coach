# Fixtures, Testing, and the GSI Replay Simulator

## Why fixtures are mandatory, not optional

Two things make GSI unusually fixture-dependent compared to typical
external-API integration code:

1. There's no official schema to test against — the only ground truth is
   real observed payloads (see `payload-structure.md`'s "needs
   verification" list). A fixture *is* the verification.
2. Correctness depends on sequences of partial payloads, not single
   payloads in isolation (`state-diffing.md`), so a single hand-written
   example often isn't enough to catch merge bugs.

Every GSI change — new field consumed, new normalization rule, new
`DomainEvent` — needs a fixture demonstrating the payload shape involved,
and a test exercising the pipeline against it.

## What a fixture is

A fixture is a captured or hand-built JSON payload (or sequence of
payloads) representing a real or realistic GSI POST body. Prefer real
captured payloads over hand-written ones when verifying a "needs
verification" field from `payload-structure.md` — a hand-written fixture
only proves the code handles what you assumed the shape to be, not that
the assumption is correct.

To capture a real fixture: point a project GSI config at a simple logging
listener during an actual game, save the raw POST bodies, then trim to the
minimal sequence needed for the test case (keep enough context —
`provider`/timestamps — that the sequence remains realistic, but strip
unrelated noise).

## Fixture organization

Store fixtures alongside the GSI test project, grouped by scenario, e.g.:

```
<test-project>/Fixtures/Gsi/
  hero-death/
    01-alive.json
    02-death-payload.json     (includes previously.hero.alive = true)
    03-respawn.json
  item-purchase/
    01-empty-slot.json
    02-item-added.json        (includes added.items.slot0)
  reconnection-gap/
    01-before-gap.json
    02-after-gap.json         (large clock_time jump, no intermediate payloads)
  spectating/
    01-multi-player-payload.json
```

Naming payloads in sequence order (`01-`, `02-`, ...) makes multi-step
scenarios self-documenting and keeps replay-simulator input trivial to
construct.

## What every GSI change must test

1. **Parsing** — the payload parses into the expected typed structure
   without error, and fields absent from the payload are left
   unset/absent in the parsed result (not defaulted to misleading zero
   values).
2. **Merge/normalization** — feeding the fixture sequence through the
   normalizer produces the expected `MatchState` after each step, not
   just after the last one (see `state-diffing.md`).
3. **Domain events** — if the change affects event derivation, assert the
   expected `DomainEvent`(s) are produced at the expected step, and that
   no spurious events are produced at other steps.
4. **FairPlay boundary** — if the change touches a field with any
   ambiguity about live-legitimacy (see `dota-fairplay`), include or
   reference the relevant FairPlay test rather than assuming it's covered
   elsewhere.

## The GSI replay simulator

The replay simulator is a small test/dev tool that takes a fixture
sequence and feeds it through the ingestion pipeline (`GsiListener` or
directly into `GsiPayloadParser`, depending on what's being exercised) at
a controlled pace — either as fast as possible for automated tests, or in
real time (respecting each fixture's timestamp deltas) for manual
debugging and demoing.

Two uses:

- **In automated tests** — replaying a fixture sequence gives exactly the
  multi-step scenario needed to test diffing/merge/event-derivation logic
  without needing a live Dota client running.
- **For manual debugging** — replaying a captured real match's fixture
  sequence in real time lets a developer reproduce a specific observed bug
  (e.g. "MatchState went wrong right after this Roshan kill") without
  needing to reproduce it live in-game, which is slow and non-deterministic
  given GSI's own reliability issues.

Implementation shape (adapt to the project's actual test framework/DI
setup):

```
ReplaySimulator
  - LoadSequence(directory) -> ordered list of raw JSON payloads
  - Run(pipelineEntryPoint, speed: Instant | RealTime)
      for each payload in sequence:
          if speed == RealTime: wait for the gap implied by provider.timestamp
          POST (or directly invoke) payload into pipelineEntryPoint
  - exposes resulting MatchState/DomainEvents after each step for assertions
```

Keep the simulator itself free of Dota-specific parsing logic — it should
just replay raw bytes through whatever entry point is under test, so it
stays usable for `PlayingGameState` and `SpectatingGameState` fixtures
alike, and doesn't itself become another place field assumptions can be
baked in incorrectly.

## Before considering GSI work complete

Run `dotnet test` (or the project's full test suite) with the new/updated
fixture-based tests included. A GSI change without a passing fixture test
is not done, regardless of how confident the field mapping looks —
GSI has no spec to fall back on, so the fixture *is* the specification for
that field going forward.

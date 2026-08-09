# GSI Fixture and Replay Simulator Tests

These are covered in depth in `dota-gsi`'s own reference files
(`fixtures-and-testing.md` there is the authoritative source) — this file
is a short pointer explaining how they fit into the overall test suite
and Definition of Done, not a restatement of the full guidance.

## Why GSI needs its own test category

GSI has no official Valve spec (per `dota-gsi`) — field names and payload
shapes are reverse-engineered from community sources. A captured or
hand-built fixture *is* the closest thing to a contract this project has
for GSI data. That makes fixture-based testing load-bearing in a way it
isn't for a typical documented API integration: without a fixture test, a
GSI parsing change has no ground truth to be checked against at all.

## GSI fixture tests

**What they prove:** parsing and normalization logic handles a single
real or realistic GSI payload correctly — the right fields land in the
right places in the typed model, absent fields stay absent rather than
defaulting to misleading values, and `PlayingGameState` vs.
`SpectatingGameState` payloads are each handled according to their actual
(different) shape.

**Required for:** any change touching GSI payload parsing, a new field
being consumed, or a new normalization rule. See `dota-gsi`'s
`fixtures-and-testing.md` for fixture format, storage location, and
naming conventions.

## GSI replay simulator tests

**What they prove:** a *sequence* of payloads is handled correctly over
time — this is where partial-payload merging bugs, `previously`/`added`
diffing bugs, stale-state detection, and reconnection-gap handling
actually get caught. A parser can pass every single-payload fixture test
and still merge incorrectly across a sequence (see `dota-gsi`'s
`state-diffing.md`), so sequence-based tests via the replay simulator are
not optional for anything touching the merge/normalization logic.

**Required for:** any change to `MatchState` merge logic, event derivation
from state transitions, or stale/reconnection handling. See `dota-gsi`'s
`fixtures-and-testing.md` for the replay simulator's design and
`stale-state-and-reconnection.md` for the specific gap-handling scenarios
worth testing.

## How this feeds the Definition of Done

Per `dota-gsi`'s own rule: a GSI change without a passing fixture test is
not done, regardless of how confident the field mapping looks. This
skill's Definition of Done (`definition-of-done.md`) incorporates that
rule directly — any change touching GSI parsing/normalization needs at
least one fixture test, and any change touching merge/diffing/staleness
logic needs at least one replay-simulator (sequence) test, before
`dotnet test` is considered a meaningful pass for that change.

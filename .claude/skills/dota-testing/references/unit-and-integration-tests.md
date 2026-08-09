# Unit and Integration Tests

## Unit tests

**What they prove:** a single unit of logic (a function, a class, a small
module) behaves correctly in isolation — given specific inputs, it
produces the expected output, independent of any infrastructure (no live
GSI connection, no real screen, no network, no running WPF app).

**Where they apply:** essentially everywhere, but especially:
- Domain logic (`dota-domain` concepts — position/lane reasoning, damage
  type mitigation, farm priority calculations).
- Feature Engine transformations (`dota-architecture`) — a specific raw
  value in, a specific normalized feature out.
- Recommendation Engine logic given a fixed feature input.
- GSI parsing/normalization functions given a single fixed payload (as
  opposed to a fixture *sequence*, which is `references/gsi-testing.md`'s
  territory).

**What makes a good one:** deterministic, fast (no I/O, no sleeps), and
testing one behavior per test so a failure immediately tells you what
broke. Per `dota-architecture`'s layering, most of this project's logic
(Domain, Feature Engine, Recommendation Engines) should be unit-testable
without any infrastructure dependency at all — if a unit test needs a
live GSI connection or a running UI to pass, that's usually a sign the
architecture boundary has already been crossed somewhere (see
`dota-architecture`'s dependency rules), not just a testing inconvenience
to work around.

## Integration tests

**What they prove:** multiple units correctly compose across a real (or
realistically faked) boundary that a unit test, by design, doesn't cross.

**Where they apply:**
- The GSI pipeline end-to-end: listener → parser → normalizer → MatchState,
  exercised together (distinct from GSI fixture tests, which focus
  specifically on payload-shape correctness — an integration test here is
  about the pipeline's stages correctly handing off to each other).
- State Merger reconciling GSI + CV observations together.
- A Recommendation Engine actually consuming real Feature Engine output
  (not a hand-constructed feature object) to catch integration-shape
  mismatches a unit test with mocked input wouldn't reveal.
- Data Platform's local caching layer actually round-tripping through
  DuckDB/Parquet (`dota-data`) rather than just testing the in-memory
  transformation logic.

**What makes a good one:** still deterministic and reasonably fast where
possible — use fixtures and local test doubles for external
dependencies (a fake HTTP server standing in for OpenDota, not a live
network call) so integration tests remain reliable in CI. Reserve genuine
external dependencies (live network calls, a real Dota client) for the
smaller set of end-to-end tests (`end-to-end-tests.md`) that are
explicitly meant to exercise them.

## Architecture tests (brief note)

`dota-architecture` defines its own dependency-graph enforcement tests
(e.g. "ML must not depend on WPF/WinUI," checked structurally). These are
a form of automated test in their own right and belong in the test
suite alongside unit/integration tests — see `dota-architecture`'s
"Enforcement" section for what they should assert. This skill doesn't
duplicate that guidance; just note that these tests exist and should run
as part of the normal `dotnet test` gate.

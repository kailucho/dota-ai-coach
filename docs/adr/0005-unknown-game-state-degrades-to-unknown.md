# ADR 0005: Unknown game_state strings degrade to Unknown, never throw

## Status

Accepted

## Context

`map.game_state` is a string enum defined by Valve
(`DOTA_GAMERULES_STATE_GAME_IN_PROGRESS`, etc.), with no official published
schema. Valve can add new states at any point (e.g. a future game mode),
and the parser must not crash the ingestion pipeline when it encounters
one it doesn't recognize.

## Decision

`GameStateParser.Parse(string?)` is a pure switch expression over the
ten currently-known values (per `docs/RESEARCH.md`), with a `_ =>
GameState.Unknown` fallback for anything else, including `null`/empty
strings. `MatchSnapshot` always retains the raw string
(`RawGameState`) alongside the parsed enum, so no information is lost
even when the enum can't represent it.

## Consequences

- A future Dota patch introducing a new game state does not crash the
  pipeline — it degrades to `Unknown` and the raw string remains
  available for debugging/future handling.
- Verified by `GameStateParserTests.Parse_never_throws_and_degrades_to_unknown`
  (theory over null/empty/garbage/future-state strings) and
  `GsiPayloadParserTests.Parse_unknown_game_state_parses_successfully_and_degrades_to_unknown`.
- The `RuleRecommendationEngine`/`FeatureEngine` treat `GameState.Unknown`
  as not-in-progress, so rules correctly stay silent rather than acting
  on an unrecognized/ambiguous game phase.

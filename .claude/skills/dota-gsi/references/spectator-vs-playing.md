# PlayingGameState vs SpectatingGameState

## Why this distinction exists

GSI's payload shape depends on what role the connected client has in the
match, and the difference is not cosmetic — it's a difference in how much
of the game state is exposed:

- **Playing** — the client is a participant. GSI exposes that player's own
  data: own hero, own items, own abilities, and map/player info limited to
  what that player's client actually knows (their own vision, not fog-of-war
  hidden state). This mirrors what the player could legitimately see anyway.
- **Spectating** — the client is observing (e.g. via observer/spectator
  mode, which historically can include broadcast-delay or all-vision
  contexts). GSI can expose data for **all ten players** across both
  teams, structured as multiple keyed player/hero blocks (e.g.
  player0..player9) rather than a single player/hero block. This is
  materially more information than any single player has access to.

Community GSI libraries (e.g. antonpup/Dota2GSI) explicitly model these as
separate types — `PlayingGameState` and `SpectatingGameState` — rather than
one shape with optional extra fields, because the structural difference
(single block vs. multi-player block) is significant enough to warrant it.
Follow that same modeling choice in this project: don't try to force both
into one shape with nullable "other player" fields bolted on.

## Why this matters for fair play

See the `dota-fairplay` skill for the full rule, but the short version
specific to GSI: **`SpectatingGameState` data must never be wired into a
live, player-facing feature.** A spectator sees more than a player
legitimately does — even omniscient-looking `map` timers or `player`
blocks that *look* like they could just be "extra convenience data" are,
by construction, information a live player wouldn't have. If a feature
needs spectator-level data, that's a strong signal it's out of scope for
live player-facing use, not a signal to find a workaround.

Concretely, in code:
- `GsiListener`/`GsiPayloadParser` should tag every parsed payload with
  which mode produced it (`PlayingGameState` vs `SpectatingGameState`) as
  part of the type itself — not as a runtime flag that's easy to ignore
  downstream.
- The normalization step feeding live `MatchState` should only accept
  `PlayingGameState` input. If `SpectatingGameState` needs to exist in this
  project at all (e.g. for a genuinely separate spectator/broadcast
  feature), it should be a structurally separate pipeline, not a superset
  that live code happens to only read part of.
- A FairPlay test (see the `dota-fairplay` skill's testing guidance)
  should assert that the live recommendation path's input type cannot be
  constructed from `SpectatingGameState` data at all — structurally
  impossible, not just conventionally avoided.

## Practical parsing implication

Because `SpectatingGameState` payloads use a different structural shape
(multi-player keyed blocks) rather than just "more fields," a naive parser
written and tested only against `PlayingGameState` fixtures may silently
mis-parse or drop data from a `SpectatingGameState` payload rather than
erroring clearly. If this project ever needs to handle both, write and test
against fixtures of both shapes explicitly (`fixtures-and-testing.md`) —
don't assume one parser generalizes to the other without verification.

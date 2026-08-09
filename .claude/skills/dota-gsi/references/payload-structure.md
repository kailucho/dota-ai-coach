# GSI Payload Structure

No official Valve spec exists for this. Everything below is
reverse-engineered from community GSI client libraries and observed
payloads (see Sources at the bottom). Sections are marked by confidence
level — treat "needs verification" sections as things to confirm against
an actual captured fixture before writing parsing/normalization code
against them, not as settled fact.

## Transport

The Dota 2 client sends an HTTP **POST** with a **JSON** body to the
configured `uri`, on state change (subject to `buffer`/`throttle`) and
periodically via `heartbeat` (see `configuration.md`). There is no
built-in retry/queueing on the client side beyond what `timeout` implies
— a listener that's briefly unavailable can simply miss updates.

## Top-level sections (well-verified)

Each corresponds to a `data` flag in the config. A payload only includes
the sections currently enabled *and* only the fields that changed or are
newly relevant — see `state-diffing.md` for what this means for parsing.

- **provider** — client metadata: name, appid, version, timestamp. Useful
  for sanity-checking you're talking to a real, current Dota client and
  for correlating payload timestamps.
- **map** — match-level state: game clock time, day/night cycle,
  game_state (what phase the match is in — e.g. hero selection, strategy
  time, in progress, post game), match id, and timers for contestable
  resources (Roshan, runes). `roshan_state`-style enum values are in the
  "needs verification" bucket below — confirm actual values against a
  fixture rather than assuming a specific enum set.
- **player** — per-player stats: kills/deaths/assists, gold, gold per
  minute / xp per minute, net worth, team assignment. In
  `PlayingGameState` this is the local player only; in
  `SpectatingGameState` this expands to cover multiple players — see
  `spectator-vs-playing.md`.
- **hero** — the hero being played (or observed): health, mana, level,
  XP, alive/respawn state and timer, talent choices, and status-effect
  flags (e.g. silenced, stunned, disarmed — exact flag set needs
  fixture confirmation for this project's purposes).
- **abilities** — one entry per ability slot: level, cooldown remaining,
  whether it's currently castable, and whether it's the ultimate slot.
- **items** — inventory slots (commonly 0-5), backpack slots, stash,
  teleport scroll slot, and neutral item slot; each entry with
  charges/cooldown where relevant.
- **buildings** — tower and barracks (and implicitly ancient) health for
  both sides, keyed by lane/position.
- **draft** — Captain's Mode pick/ban phase state: active team, current
  pick/ban, timers. Only meaningful in game modes that have a formal draft
  phase — needs verification: exact field names and behavior in
  non-Captain's-Mode game modes.
- **wearables** — equipped cosmetic item IDs. Rarely relevant to gameplay
  logic; low priority to support unless a specific feature needs it.

## Needs verification before relying on it

Flagging these explicitly per the "never invent GSI fields" rule — don't
write normalization code against exact field names/enum values here
without confirming against a real fixture first:

- **`events`** — Dota GSI is known to support an events array (e.g. kill
  events, Roshan events) but the exact schema is inconsistently documented
  across community sources. Capture a real fixture covering the event(s)
  you need before coding against a specific shape.
- **`roshan_state`** enum values under `map` — presence is well attested,
  exact string/enum values are not consistently documented.
- **`draft`** field names — presence is attested but exact key names
  should be confirmed against a captured Captain's Mode payload.
- Exact **status-effect flags** under `hero` (stunned/silenced/etc.) —
  confirm the actual key names in a fixture rather than assuming a
  specific naming convention.

If a task needs one of these and no fixture exists yet, capturing one
(see `fixtures-and-testing.md`) is a prerequisite, not an optional
follow-up.

## Known reliability issues

Documented via Valve's own issue tracker (ValveSoftware/Dota-2 #1023,
long-standing and unresolved) and consistent with community reports:

- The connection can fail to establish at all on some game launches,
  independent of whether the listener is ready.
- Even once connected, the first payloads can be delayed relative to
  match/game start — don't assume data arrives the instant a match begins.
- A previously-working connection can silently stop sending updates
  without an explicit disconnect signal — this is why `heartbeat` and
  active staleness detection matter (`stale-state-and-reconnection.md`),
  rather than trusting "no error" to mean "still connected."
- Behavior is inconsistent across sessions/launches — code defensively;
  don't treat any particular reliability behavior as guaranteed.

## Sources

- xzion/dota2-gsi (GitHub)
- antonpup/Dota2GSI (GitHub), including PR #19 (PlayingGameState vs
  SpectatingGameState split)
- ValveSoftware/Dota-2 GitHub issue #1023
- Community GSI intro write-ups (e.g. auo.nu "Game State Integration
  Intro")

These are reverse-engineered, actively-maintained-by-the-community
sources, not a Valve-published contract. Re-verify against current
sources/fixtures periodically, especially after major Dota patches.

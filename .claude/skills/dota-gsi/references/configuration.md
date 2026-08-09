# GSI Configuration

## Where the config lives

Dota 2 reads GSI config files from:

```
<Dota 2 install>/game/dota/cfg/gamestate_integration/gamestate_integration_<name>.cfg
```

The filename **must** start with `gamestate_integration_` — Dota only
scans files matching that prefix in that directory. `<name>` is arbitrary
and only matters for distinguishing multiple configs on one machine.

This file is deployed to the player's Dota install, not shipped inside
this project's own build output — treat instructions/tooling for
generating or installing it as a setup step, not a runtime dependency the
app can assume is already present.

## File format

GSI config is Valve's KeyValues format (same as other Source-engine
config), not JSON:

```
"Dota AI Coach GSI"
{
    "uri"           "http://localhost:PORT/"
    "timeout"       "5.0"
    "buffer"        "0.1"
    "throttle"      "0.1"
    "heartbeat"     "30.0"
    "data"
    {
        "provider"    "1"
        "map"         "1"
        "player"      "1"
        "hero"        "1"
        "abilities"   "1"
        "items"       "1"
        "buildings"   "1"
        "draft"       "1"
        "wearables"   "1"
    }
}
```

## Field meanings

- **uri** — the HTTP endpoint the game client POSTs to. Typically a
  `localhost` URI pointing at this project's `GsiListener`. Port must
  match whatever the listener actually binds.
- **timeout** (seconds) — how long the client waits for an HTTP 2xx
  response before treating the request as failed/timed out. Keep the
  listener fast; a slow listener can cause the game client to back off.
- **buffer** (seconds) — a coalescing window: multiple state changes
  within this window get merged into a single POST rather than firing one
  per change. Lower values mean more frequent, smaller updates; higher
  values mean fewer, larger, slightly-delayed updates.
- **throttle** (seconds) — minimum interval enforced between successive
  POSTs, independent of buffering. Prevents flooding the listener during
  rapid state churn (e.g. a teamfight with many simultaneous changes).
- **heartbeat** (seconds) — forces a POST at this interval even if
  nothing changed. This is the mechanism for detecting "GSI is still
  connected but nothing's happening" vs. "GSI has silently stopped" — see
  `stale-state-and-reconnection.md`. Don't set this so high that stale
  detection becomes sluggish, or so low that it adds meaningful load.
- **data** — a set of boolean (`"1"`/`"0"`) flags selecting which payload
  sections the client will send. Only request sections the application
  actually consumes — each enabled section adds payload size and parsing
  surface. See `payload-structure.md` for what each section contains.
- **auth** (optional, not shown above) — a block like
  `"auth" { "token" "..." }` that gets echoed back in every payload under
  an `auth` key, letting the listener reject payloads without the
  expected token. Worth using if the listener binds to anything more
  exposed than strict `localhost`.

## Practical notes

- Changing this file requires a Dota 2 client restart (or at least a
  restart of GSI's config scan) to take effect — don't expect live reload.
- If multiple GSI configs are present with overlapping `data` sections,
  behavior across them is not something to assume without testing — avoid
  shipping more than one active config for this project on a given
  machine unless there's a concrete reason to split them.
- Because this file lives outside the project's own deployable artifact,
  any setup script that generates/installs it should be idempotent and
  should not silently overwrite a differently-configured file a developer
  may already have for other tools.

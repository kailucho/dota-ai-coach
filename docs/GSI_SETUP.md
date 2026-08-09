# GSI Setup

To feed a live Dota 2 match into this application, install a Game State
Integration config file so the Dota 2 client POSTs match state to this
app's listener.

## 1. Enable the launch option

In Steam: right-click Dota 2 → Properties → Launch Options, add:

```
-gamestateintegration
```

Without this, Dota will never send GSI payloads regardless of a correctly
configured `.cfg` file.

## 2. Install the config file

Create (or copy) the following file to:

```
<Steam install>\steamapps\common\dota 2 beta\game\dota\cfg\gamestate_integration\gamestate_integration_dota_ai_coach.cfg
```

The filename must start with `gamestate_integration_` — Dota only scans
files matching that prefix in that directory.

Contents:

```
"Dota AI Coach"
{
    "uri"           "http://127.0.0.1:3000/gsi"
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
    }
}
```

This matches `DotaAiCoach.Gsi.GsiOptions`'s defaults (port 3000, route
`/gsi`). If you change `GsiOptions.Port`/`Route` in code, update the
`uri` here to match.

## 3. Restart Dota 2

GSI config changes are read at client startup — a running Dota 2 client
will not pick up a newly added/edited config file without a restart.

## 4. Verify

Run the overlay (`dotnet run --project src/DotaAiCoach.UiOverlay`), then
start a Dota 2 match (bot match is fine for testing). The overlay should
begin showing your hero name, clock, KDA, gold, GPM/XPM, and items within
a few seconds of the match starting.

If nothing appears:
- confirm `-gamestateintegration` is in Dota's launch options;
- confirm the `.cfg` file's `uri` port matches what the overlay is
  listening on (check the overlay's status text, or console output if
  running the Host/CoachSession directly);
- confirm no firewall is blocking loopback traffic on port 3000 (rare,
  but possible with some security software).

## Testing without a live Dota client

Use the replay simulator against hand-built or captured fixtures instead
of playing a real match — see `README.md`'s "Running the replay
simulator" section.

# Dota AI Coach

A Windows desktop application that coaches a Dota 2 player in real time
using exclusively information legitimately available to that player —
their own Game State Integration (GSI) feed while playing. See
`docs/MASTER_PLAN.md` for the full product vision and `docs/ROADMAP.md`
for what's actually implemented so far.

**This iteration delivers**: Fase 0 (research/foundation) + Fase 1 (data
platform, reduced to what the vertical slice needs) + a working vertical
slice — `Dota GSI → MatchState → State Engine → Rule Recommendation
Engine → Overlay → Tests`.

## Prerequisites

- Windows (the overlay is a WPF app, `net8.0-windows`)
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)

## Build

```powershell
dotnet build DotaAiCoach.sln -c Debug
```

## Test

```powershell
dotnet test DotaAiCoach.sln -c Debug
```

Run a single test project:

```powershell
dotnet test tests/DotaAiCoach.Gsi.Tests -c Debug
```

## Run the overlay

```powershell
dotnet run --project src/DotaAiCoach.UiOverlay
```

This starts a GSI listener on `http://127.0.0.1:3000/gsi` and shows the
overlay window. To actually see live match data, install the GSI config
per `docs/GSI_SETUP.md` first.

Pass `--click-through` to enable click-through/non-activating window
behavior (off by default so the window stays interactive for
development):

```powershell
dotnet run --project src/DotaAiCoach.UiOverlay -- --click-through
```

## Player Profile (Fase 2)

Click **Profile** on the overlay window to open the player profile
dashboard (Hero Comfort Score + Dota DNA), sourced from your public
OpenDota match history. Two things to know:

- **Finding your Steam ID**: if Dota is running and GSI is connected, the
  field pre-fills automatically. Otherwise, paste either your Steam64 ID
  (from your Steam profile URL, e.g. `steamcommunity.com/profiles/<this
  number>`) or your OpenDota account_id (Steam32) — either form works.
- **"Match history is private"**: OpenDota can only see matches for
  accounts with **Expose Public Match Data** enabled. In Dota 2, go to
  Settings → Options → Advanced and enable it, then play (or wait for)
  a match — OpenDota indexes new matches shortly after they finish, not
  retroactively. Click Refresh again once indexed.

A normal **Refresh** only computes Farm/Fighting/Survival/Late Game DNA
dimensions from your match summaries. Click **Deep DNA Refresh** to also
unlock Laning, Objectives, Vision, Itemization, Buyback Discipline, and
Tempo — this parses up to 20 of your recent ranked matches on OpenDota,
which is slower and uses noticeably more of OpenDota's rate limit than a
normal refresh, so use it sparingly rather than on every session.
Positioning has no reliable data source yet and stays unavailable either
way.

## Running the replay simulator

Drive the pipeline from fixture files instead of a live Dota client —
useful for development and manual debugging.

In-process (prints state after each step, no network involved):

```powershell
dotnet run --project tools/DotaAiCoach.ReplaySimulator -- `
  --fixtures tests/DotaAiCoach.Gsi.Tests/Fixtures/Gsi/laning-sequence `
  --speed fixed --interval 1000 --print-state
```

Against a running overlay, over real HTTP (start the overlay first):

```powershell
dotnet run --project tools/DotaAiCoach.ReplaySimulator -- `
  --fixtures tests/DotaAiCoach.Gsi.Tests/Fixtures/Gsi/laning-sequence `
  --http http://127.0.0.1:3000/gsi --speed realtime
```

Speed options: `instant` (no delay, default), `fixed` (fixed interval
per `--interval` ms), `realtime` (paced by each fixture's own
`provider.timestamp` delta).

## Project layout

See `docs/ARCHITECTURE.md` for the full project graph and rationale.
Summary:

```
src/DotaAiCoach.FairPlay/              FairPlay classification system
src/DotaAiCoach.Domain/                MatchState, domain events, recommendation types
src/DotaAiCoach.Gsi/                   GSI listener, parser, normalizer, replay simulator core
src/DotaAiCoach.FeatureEngine/         MatchState -> LiveFeatureSet (the recommendation layer's only input)
src/DotaAiCoach.RecommendationEngines/ Rule-based recommendation engine + anti-spam message budget
src/DotaAiCoach.PlayerProfile/         Hero Comfort Score + Dota DNA scoring (pure, no HTTP)
src/DotaAiCoach.OpenDota/              OpenDota HTTP client, RAW JSON cache, rate limiter
src/DotaAiCoach.Host/                  Composes both pipelines; the only surface UiOverlay may depend on
src/DotaAiCoach.UiOverlay/             WPF overlay window + player profile window
tools/DotaAiCoach.ReplaySimulator/     CLI wrapper for the replay simulator
tools/DotaAiCoach.GsiCapture/          Standalone tool for recording real GSI payloads to fixtures
tests/                                 One test project per src project, plus Architecture.Tests
docs/                                  Research, architecture, fair play, roadmap, ADRs
```

## Documentation

- `docs/RESEARCH.md` — GSI field research and sourcing
- `docs/ARCHITECTURE.md` — project graph and design rationale
- `docs/FAIR_PLAY.md` — data classification and how it's enforced
- `docs/ROADMAP.md` — what's delivered, limitations, next phase
- `docs/GSI_SETUP.md` — installing the GSI config for a live match
- `docs/adr/` — architecture decision records
- `docs/MASTER_PLAN.md` — the full product vision (most of it out of scope for this iteration)

## Coding standards

C#: nullable enabled, warnings-as-errors, `net8.0`/`net8.0-windows`. See
`Directory.Build.props`.

# Architecture

This document describes the architecture actually implemented in this
iteration (Fase 0 + Fase 1 + the vertical slice), and how it maps onto
`.claude/skills/dota-architecture`'s full intended long-term layout. See
`docs/ROADMAP.md` for what's deferred.

## Project graph

```
DotaAiCoach.sln
├── src/
│   ├── DotaAiCoach.FairPlay/              → (no references)
│   ├── DotaAiCoach.Domain/                → FairPlay
│   ├── DotaAiCoach.PlayerProfile/         → FairPlay
│   ├── DotaAiCoach.FeatureEngine/         → Domain, FairPlay
│   ├── DotaAiCoach.Gsi/                   → Domain, FairPlay
│   ├── DotaAiCoach.OpenDota/              → PlayerProfile, FairPlay
│   ├── DotaAiCoach.RecommendationEngines/ → Domain, FeatureEngine, FairPlay
│   ├── DotaAiCoach.Host/                  → Gsi*, Domain, FeatureEngine*, RecommendationEngines*, OpenDota*, PlayerProfile
│   └── DotaAiCoach.UiOverlay/             → Domain, PlayerProfile, Host   [net8.0-windows, WPF]
├── tools/
│   └── DotaAiCoach.ReplaySimulator/       → Gsi, Domain  (thin CLI wrapper)
└── tests/
    ├── DotaAiCoach.Domain.Tests/
    ├── DotaAiCoach.Gsi.Tests/             (includes Fixtures/Gsi/)
    ├── DotaAiCoach.FeatureEngine.Tests/
    ├── DotaAiCoach.RecommendationEngines.Tests/
    ├── DotaAiCoach.PlayerProfile.Tests/
    ├── DotaAiCoach.OpenDota.Tests/        (includes Fixtures/, real OpenDota captures)
    ├── DotaAiCoach.FairPlay.Tests/
    └── DotaAiCoach.Architecture.Tests/    (also references Gsi/PlayerProfile/OpenDota, for reflection)
```

`*` = referenced with `PrivateAssets="compile"` in `Host.csproj` — these
references are consumed inside `Host` but deliberately do not flow
transitively to `UiOverlay`'s compile-time surface (runtime DLLs still
propagate; see the comment in `Host.csproj` for why `"compile"` and not
`"all"`). This is what makes ADR-0008's rule ("UI must not see GSI wire
types") and ADR-0012's equivalent for OpenDota enforced facts rather than
conventions — see
`DotaAiCoach.Architecture.Tests.DependencyGraphTests.UiOverlay_references_no_Gsi_directly_or_transitively`
and `.UiOverlay_references_no_OpenDota_directly_or_transitively`.

This graph is a subset of the full layout in
`.claude/skills/dota-architecture/references/dotnet-solution-structure.md`,
with identical project names so later phases' projects
(`ComputerVision`, `MlInference`, `DataPlatform`, `Training`,
`RecommendationOrchestrator`) drop in without renames.

## Data flow (live pipeline)

```
Dota 2 client
  → GSI HTTP POST (127.0.0.1:3000/gsi)
  → DotaAiCoach.Gsi.Listener.GsiListener       (Kestrel, loopback-only)
  → DotaAiCoach.Gsi.Parsing.GsiPayloadParser   (raw JSON → typed, nullable wire model)
  → DotaAiCoach.Gsi.Normalization.GsiNormalizer (field-wise merge → MatchState, derives DomainEvents)
  → DotaAiCoach.Gsi.Replay.GsiPipeline         (owns current MatchState + FeedHealth)
  → DotaAiCoach.FeatureEngine.FeatureEngine    (MatchState + FeedHealth + events → LiveFeatureSet)
  → DotaAiCoach.RecommendationEngines.RuleRecommendationEngine (LiveFeatureSet → Recommendation[])
  → DotaAiCoach.RecommendationEngines.LiveMessageBudget (Recommendation[] → LiveMessage[], capped/anti-spammed)
  → DotaAiCoach.Host.CoachSession               (composes the above, exposes ICoachSession)
  → DotaAiCoach.UiOverlay                       (WPF, binds ICoachSession's Domain-only output)
```

## Why `DotaAiCoach.Host` exists

Per `dota-architecture`'s dependency rules, the UI layer must not see GSI
wire types or call recommendation internals directly. `Host` is the
structural firewall: it owns the composition of listener → pipeline →
feature engine → rule engine → budget, and exposes exactly one interface,
`ICoachSession`, built entirely from `DotaAiCoach.Domain` types:

```csharp
public interface ICoachSession
{
    MatchState CurrentState { get; }
    FeedHealth Health { get; }
    IReadOnlyList<LiveMessage> ActiveMessages { get; }
    event EventHandler<CoachUpdate>? Updated;
    Task StartAsync(CancellationToken ct);
    Task StopAsync(CancellationToken ct);
}
```

`CoachSessionFactory.Create(...)` is the only construction entry point
`UiOverlay` calls — it never references `DotaAiCoach.Gsi`,
`DotaAiCoach.FeatureEngine`, or `DotaAiCoach.RecommendationEngines` types
directly, and `Host.csproj` marks those references `PrivateAssets="all"`
so this holds even under .NET's default transitive-reference behavior,
not just by convention.

## Why `DotaAiCoach.FeatureEngine` exists as its own project

`dota-architecture`'s dependency rule 6 requires recommendation engines
to consume normalized features, never raw `MatchState` directly. This
project is the mandatory seam: it centralizes staleness/game-state
usability logic (`LiveFeatureSet.IsUsable`) once, rather than letting
every rule re-derive it, and gives rules a stable, engine-agnostic input
shape.

## Where the anti-spam / message-budget logic lives

Per `dota-live-coach`, live output must be capped (1-3 messages),
priority-tiered (P0/P1/P2), and rate-limited via cooldowns and change
thresholds. In this slice — with only one recommendation engine — that
logic lives in `DotaAiCoach.RecommendationEngines.LiveMessageBudget`.
When a real `RecommendationOrchestrator` project exists (multiple
engines competing for the same message budget), this class should move
there wholesale; it's written to be self-contained specifically so that
move is mechanical. See `docs/ROADMAP.md`.

## The parallel Host firewall for OpenDota (Fase 2)

`DotaAiCoach.Host` also exposes `IProfileSession` /
`ProfileSessionFactory`, mirroring `ICoachSession`/`CoachSessionFactory`
exactly: `OpenDota` is referenced with `PrivateAssets="compile"`,
`PlayerProfile` is a normal reference (its types are `IProfileSession`'s
public surface), and `UiOverlay` only ever references `PlayerProfile`.
See `docs/adr/0012` for the full reasoning and `docs/FAIR_PLAY.md` for the
classification of the profile data itself.

## FairPlay classification in this slice

Every live-facing Domain/FeatureEngine type is attributed with
`[FairPlaySource(...)]` (see `DotaAiCoach.FairPlay`). Everything in this
slice is sourced from the local player's own GSI feed while playing (not
spectating), so every classified type is `LiveAllowed`. See
`docs/FAIR_PLAY.md` for the full classification and
`DotaAiCoach.FairPlay.Tests` for the mechanical enforcement (no
`Forbidden`/`SpectatorOnly` type is reachable from the live
recommendation path).
